namespace StrideLink.Input;

public interface IMovementOutput : IDisposable
{
    bool IsMoving { get; }
    void SetMoving(bool active);
}
