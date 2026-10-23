using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;

namespace STEX.Inv.Desktop.Infrastructure.Services;

public sealed class OperatorSession : IOperatorSession
{
    public Operario? Current { get; private set; }

    public bool IsSignedIn => Current is not null;

    public void SignIn(Operario operario)
    {
        Current = operario;
    }

    public void SignOut()
    {
        Current = null;
    }
}
