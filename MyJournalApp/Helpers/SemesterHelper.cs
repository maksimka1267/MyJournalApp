namespace MyJournalApp.Helpers;

public static class SemesterHelper
{
    public static (DateTime Start, DateTime End) GetPeriod(int year, int semester)
    {
        return semester switch
        {
            1 => (
                new DateTime(year, 9, 1),
                new DateTime(year, 12, 31)
            ),

            2 => (
                new DateTime(year + 1, 1, 1),
                new DateTime(year + 1, 6, 30)
            ),

            _ => throw new ArgumentException("Invalid semester")
        };
    }
}