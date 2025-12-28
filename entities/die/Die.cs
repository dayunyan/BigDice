using Godot;
using System;

// 确保类名和文件名一致
public partial class Die : RigidBody2D
{
    // [Export] 允许你在编辑器里直接拖拽20张图片进去
    [Export] public Texture2D[] DieTextures; 
    
    // 当前骰子的等级（0代表第1张图，19代表第20张）
    public int Level { get; private set; } = 0;

    private Sprite2D _sprite;
    private CollisionPolygon2D _collider;
    // 标记是否正在被销毁，防止多次触发合并
    private bool _isDespawning = false;

    // 定义20种颜色 (Hex代码)
    // 顺序：红->橙->黄->绿->青->蓝->紫->粉->深色/特殊色
    private readonly Color[] _levelColors = new Color[]
    {
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

    public override void _Ready()
    {
        // 这里保留赋值，作为双重保险
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _collider = GetNode<CollisionPolygon2D>("CollisionPolygon2D");
        
        ContactMonitor = true; 
        MaxContactsReported = 4;
        BodyEntered += OnBodyEntered;
    }

    // 初始化方法
    public void Setup(int level, Vector2 position, Texture2D[] textures)
    {
        Level = level;
        GlobalPosition = position;
        DieTextures = textures; // 传递纹理引用

        UpdateVisuals(); 
    }

    // --- 新增：专门用于合成后的新球入场动画 ---
    public void PlaySpawnAnimation()
    {
        // 1. 获取目标大小（UpdateVisuals 已经算好了赋值给 Sprite 了）
        Vector2 targetScale = _sprite.Scale;

        // 2. 先把视觉设为 0
        _sprite.Scale = Vector2.Zero;

        // 3. 创建 Tween 动画
        Tween tween = CreateTween();
        // 使用 Elastic (弹性) 或 Back (回弹) 效果，让它看起来像“蹦”出来的
        tween.TweenProperty(_sprite, "scale", targetScale, 0.4f)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.Out);
    }

    // --- 新增：旧球的退场动画 ---
    public void AnimateDespawn()
    {
        if (_isDespawning) return;
        _isDespawning = true;

        // 1. 立即禁用物理碰撞，防止它干扰新生成的球
        // 必须使用 SetDeferred，因为在物理回调中不能直接改变物理状态
        _collider.SetDeferred("disabled", true);
        SetDeferred("freeze", true); // 冻结位置

        // 2. 创建收缩动画
        Tween tween = CreateTween();
        // 在 0.15秒内 缩放到 0
        tween.TweenProperty(this, "scale", Vector2.Zero, 0.15f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);

        // 3. 动画结束后销毁
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void UpdateVisuals()
    {
        // --- 修复 Crash 的关键代码 ---
        // 如果 _sprite 还没赋值（因为 Setup 在 _Ready 之前运行了），尝试手动获取
        if (_sprite == null) 
        {
            _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        }
        if (_collider == null)
        {
            _collider = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
        }

        // 如果获取后还是空的（极其罕见的情况），直接返回，不做操作
        if (_sprite == null || _collider == null) return;
        
        if (DieTextures != null && Level < DieTextures.Length)
        {
            _sprite.Texture = DieTextures[Level];
            _sprite.Modulate = _levelColors[Level];
        }
        else
        {
            _sprite.Modulate = Colors.White; // 超出等级变白
        }

        // 根据等级设置体积，这里用简单公式，你可以根据需求调整
        // 假设基础大小是 1.0，每级增加 0.05
        float scale = 0.12f + (Level * 0.05f);
        _sprite.Scale = new Vector2(scale, scale);
        _collider.Scale = new Vector2(scale, scale);
        
        // 质量随等级增加
        Mass = 1.0f + (Level * 0.5f);
    }

    private void OnBodyEntered(Node body)
    {
        if (_isDespawning) return; // 如果正在销毁，不处理碰撞

        // 1. 确保撞到的是另一个 Die
        if (body is Die otherDie)
        {
            // 2. 确保没有被销毁
            if (IsQueuedForDeletion() || otherDie.IsQueuedForDeletion()) return;

            // 3. 等级相同且未达到最大级
            if (Level == otherDie.Level && Level < DieTextures.Length - 1)
            {
                // 4. 只由 ID 较小的那个负责合成，防止生成两个
                if (GetInstanceId() < otherDie.GetInstanceId())
                {
                    CallDeferred(nameof(Merge), otherDie);
                }
            }
        }
    }

    private void Merge(Die otherDie)
    {
        Vector2 centerPos = (GlobalPosition + otherDie.GlobalPosition) / 2;
        int nextLevel = Level + 1;

        // 通知 GameManager 生成新球
        // 假设主节点叫 "GameManager"，并在 Group 中 (下文会教你怎么设置 Group)
        GetTree().CallGroup("GameManager", "SpawnMergedDie", nextLevel, centerPos);

        // 销毁旧球
        otherDie.AnimateDespawn();
        this.AnimateDespawn();
    }
}