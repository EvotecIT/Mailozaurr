namespace Mailozaurr;

public partial class ClientSmtp
{
    ~ClientSmtp()
    {
        Dispose(false);
    }

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

