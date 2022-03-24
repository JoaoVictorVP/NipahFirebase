namespace NipahFirebase.FirebaseCore;

public static class NipahDatabaseExtensions
{
    public static ValueTask<T> AsValueTask<T>(this T any) => new ValueTask<T>(any);
}