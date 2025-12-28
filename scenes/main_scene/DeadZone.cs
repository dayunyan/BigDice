using Godot;
using System;
using System.Collections.Generic; // 必须引用这个才能用 List

public partial class DeadZone : Area2D
{
    // 定义信号，通知外部游戏结束
    [Signal]
    public delegate void GameOverEventHandler();

    private List<RigidBody2D> _bodiesInZone = new List<RigidBody2D>();
    private float _timer = 0f;
    private const float TIME_TO_LOSE = 2.0f; // 停留2秒判负

    public override void _Ready()
    {
        // 代码连接信号的方法：
        // 意思是：当有物体进入这个 Area2D 时，执行 OnBodyEntered 方法
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        if (_bodiesInZone.Count > 0)
        {
            bool danger = false;
            // 检查区域内是否有球基本静止了
            foreach (var body in _bodiesInZone)
            {
                // 如果球的速度小于 10 像素/秒，认为它停住了
                if (body.LinearVelocity.Length() < 10f)
                {
                    danger = true;
                    break;
                }
            }

            if (danger)
            {
                _timer += (float)delta;
                if (_timer >= TIME_TO_LOSE)
                {
                    EmitSignal(SignalName.GameOver);
                    _timer = 0; // 重置防止多次触发
                    SetProcess(false); // 停止检测
                }
            }
            else
            {
                _timer = 0;
            }
        }
        else
        {
            _timer = 0;
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body is RigidBody2D rb)
        {
            _bodiesInZone.Add(rb);
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body is RigidBody2D rb && _bodiesInZone.Contains(rb))
        {
            _bodiesInZone.Remove(rb);
        }
    }
}