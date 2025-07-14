namespace Mailozaurr;

public partial class ClientSmtp
{
    /// <summary>
    /// Finalizer to ensure the client is properly disposed.
    /// </summary>
    ~ClientSmtp()
    {
        Dispose(false);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the client and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing">If set to <c>true</c> the method has been invoked directly or indirectly by a user's code.</param>
    protected override void Dispose(bool disposing)
    {
        if (IsConnected)
        {
            try
            {
                Disconnect(true);
            }
            catch
            {
                // ignore errors while disposing
            }
        }
        base.Dispose(disposing);
    }
}

