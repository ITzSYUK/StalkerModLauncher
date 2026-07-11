namespace StalkerModLauncher.Services;

internal sealed class UsvfsSessionReservation : IDisposable
{
    private static int _activeSession;
    private int _released;

    private UsvfsSessionReservation()
    {
    }

    public static UsvfsSessionReservation Acquire()
    {
        if (Interlocked.CompareExchange(ref _activeSession, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Другой профиль уже запущен через USVFS. Завершите его перед запуском второго USVFS-профиля.");
        }

        return new UsvfsSessionReservation();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            Interlocked.Exchange(ref _activeSession, 0);
        }
    }
}
