using Godot;
using System;

public partial class GameManager : Node2D
{
    [Export] public PackedScene DieScene; 
    [Export] public Node2D Launcher;      
    [Export] public Texture2D[] AllTextures; 
    [Export] public TextureRect NextDiePreview; 
    [Export] public Control GameOverUI;   
    [Export] public Button RestartButton;
    [Export] public PackedScene ExplosionScene;
    [Export] public Label ScoreLabel;

    private int _nextLevel = 0;
    private bool _canShoot = true;
    private float _shootForce = 2500.0f; 
    private readonly Color[] _dieColors = {
        new Color("FF5252"), // 0: 鲜红
        new Color("FF7043"), // 1: 深橙
        new Color("FFCA28"), // 2: 琥珀黄
        new Color("D4E157"), // 3: 酸橙绿
        new Color("66BB6A"), // 4: 鲜绿
        new Color("26A69A"), // 5: 蓝绿
        new Color("29B6F6"), // 6: 天蓝
        new Color("42A5F5"), // 7: 这里的蓝
        new Color("5C6BC0"), // 8: 靛蓝
        new Color("7E57C2"), // 9: 深紫
        new Color("AB47BC"), // 10: 紫红
        new Color("EC407A"), // 11: 艳粉
        new Color("FF1744"), // 12: 猩红
        new Color("00E676"), // 13: 荧光绿
        new Color("00B0FF"), // 14: 荧光蓝
        new Color("651FFF"), // 15: 荧光紫
        new Color("3D5AFE"), // 16: 宝蓝
        new Color("1DE9B6"), // 17: 青色
        new Color("FFD740"), // 18: 金色
        new Color("212121")  // 19: 黑色(大西瓜/终极)
    };
    
    // 用来记录上一帧鼠标是否按下的状态
    private bool _isMouseDown = false;
    private int _score = 0;

    public override void _Ready()
    {
        AddToGroup("GameManager");
        
        // --- 安全检查 ---
        if (DieScene == null || Launcher == null || NextDiePreview == null || GameOverUI == null)
        {
            GD.PrintErr("错误：GameManager 的 Export 变量未完全赋值！请检查 Inspector 面板。");
            return;
        }
        // ----------------

        GameOverUI.Visible = false;
        RandomizeNextDie();

        if (RestartButton != null)
        {
            RestartButton.Pressed += OnRestartPressed;
        }

        _score = 0;
        UpdateScore(0);
    }

    public override void _Process(double delta)
    {
        // 如果游戏结束或暂停，不处理任何逻辑
        if (GameOverUI.Visible) return;

        // 获取当前鼠标/手指状态
        bool currentMouseDown = Input.IsMouseButtonPressed(MouseButton.Left);

        // 1. 只有当按住鼠标时，发射器才旋转跟随 (可选：如果你希望始终跟随，就把 LookAt 移到 if 外面)
        // 这里我设定为：始终跟随鼠标
        Vector2 mousePos = GetGlobalMousePosition();
        Launcher.LookAt(mousePos);

        // 2. 检测“松开鼠标”的一瞬间
        // 逻辑：上一帧是按下的(true)，这一帧没按下(false) -> 说明刚刚松手了
        if (_isMouseDown && !currentMouseDown)
        {
            if (_canShoot)
            {
                ShootDie();
            }
        }

        // 更新状态供下一帧使用
        _isMouseDown = currentMouseDown;
    }

    // 删除 _UnhandledInput 方法，不再需要它

    private void ShootDie()
    {
        GD.Print($"发射等级: {_nextLevel}"); // 添加日志，方便调试
        _canShoot = false;

        // 实例化球
        var die = DieScene.Instantiate<Die>();
        
        // 关键修改：添加到当前场景的根节点，而不是 GetParent()
        // 这样可以保证物理层级最稳定
        GetTree().CurrentScene.AddChild(die);
        
        die.GlobalPosition = Launcher.GlobalPosition;
        die.Setup(_nextLevel, Launcher.GlobalPosition, AllTextures);

        Vector2 direction = Vector2.Right.Rotated(Launcher.Rotation);
        die.ApplyImpulse(direction * _shootForce);

        RandomizeNextDie();
        
        GetTree().CreateTimer(0.5f).Timeout += () => _canShoot = true;
    }

    private void RandomizeNextDie()
    {
        _nextLevel = GD.RandRange(0, 3);
        if (AllTextures != null && AllTextures.Length > _nextLevel)
        {
            NextDiePreview.Texture = AllTextures[_nextLevel];
            NextDiePreview.Modulate = _dieColors[_nextLevel];
        }
    }

    // 新增更新分数的方法
    private void UpdateScore(int addedPoints)
    {
        _score += addedPoints;
        if (ScoreLabel != null)
        {
            ScoreLabel.Text = $"Score: {_score}";
        }
    }

    public void SpawnMergedDie(int level, Vector2 pos)
    {
        var die = DieScene.Instantiate<Die>();
        // 使用 CallDeferred 是为了在物理计算过程中安全地添加子节点
        GetTree().CurrentScene.CallDeferred("add_child", die);
        
        die.Setup(level, pos, AllTextures);

        // --- 新增：播放入场动画 ---
        die.CallDeferred("PlaySpawnAnimation"); 
        
        Vector2 randomImpulse = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized() * 200;
        die.CallDeferred("apply_impulse", randomImpulse);
        
        // 生成粒子特效 (新增逻辑)
        if (ExplosionScene != null)
        {
            var explosion = ExplosionScene.Instantiate<GpuParticles2D>();
            explosion.GlobalPosition = pos;
            
            // 设置粒子颜色与新球颜色一致
            // 注意：这里用 level 可能会越界，因为合成出来的是 level (比如 0+0=1级)
            // 但我们想展示的是合成后的颜色，所以用 level 对应的颜色
            if (level < _dieColors.Length)
            {
                explosion.Modulate = _dieColors[level];
            }
            // --- 关键优化：设置 ZIndex 防止被遮挡 ---
            explosion.ZIndex = 10; // 确保它画在所有骰子(默认Z=0)上面
            
            // 添加到场景
            GetTree().CurrentScene.AddChild(explosion);
        }

        // 计分逻辑：合成出 level 级的球，得多少分？
        // 假设：合成出1级球得10分，2级球得20分...
        // level 此时已经是合成后的等级了
        int points = (level + 1) * 10; 
        UpdateScore(points);
    }

    public void OnGameOver()
    {
        GD.Print("游戏结束");
        GameOverUI.Visible = true;
        // 注意：如果你用了 Pause，可能会导致 _Process 停止运行，所以要小心使用
        // 如果你希望背景停止动，但还能点击按钮，需要把按钮的 Process Mode 设为 When Paused
        GetTree().Paused = true; 
    }

    private void OnRestartPressed()
    {
        GD.Print("执行重启逻辑...");
        
        // 3. 关键步骤：必须先解除暂停，否则新场景加载后依然是暂停状态！
        GetTree().Paused = false; 
        
        // 4. 重新加载当前场景
        GetTree().ReloadCurrentScene();
    }
}