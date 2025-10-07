using UnityEngine;  // <--- required for Debug

// Goalkeeper jump
public class JumpCommand : ICommand
{
    private readonly IKeepable _keeper;

    public JumpCommand(IKeepable keeper) => _keeper = keeper;

    public void Execute()
    {
        if (_keeper == null) return;
        EventManager.instance.EventKeeperJump(_keeper);
    }
}