namespace MyJournalApp.Interface
{
    public interface IStudentRepository : IRepository<Student>
    {
        Task<IEnumerable<Student>> GetByGroupIdAsync(Guid groupId);
    }

}
