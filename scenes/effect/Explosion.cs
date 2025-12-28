using Godot;

public partial class Explosion : GpuParticles2D
{
    public override void _Ready()
    {
        Emitting = true; // 进场自动播放
        Finished += OnFinished; // 订阅播放结束信号
    }

    private void OnFinished()
    {
        QueueFree(); // 播放完自我销毁
    }
}