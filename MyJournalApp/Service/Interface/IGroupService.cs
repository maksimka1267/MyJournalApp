using MyJournalApp.Dtos.Group;
using MyJournalApp.Result;

public interface IGroupService
{
    Task<IEnumerable<Group>> GetAllAsync();

    Task<IEnumerable<Group>> GetTeacherGroupsAsync(Guid teacherId);

    Task<Group?> GetStudentGroupAsync(Guid studentId);

    Task<IEnumerable<User>> GetUsersByGroupAsync(Guid groupId);

    Task<Group?> GetByIdAsync(Guid id);

    Task<IServiceResult> MoveStudentAsync(MoveStudentDto dto);

    Task<ServiceResult<Group>> CreateAsync(Group group);

    Task<ServiceResult<Group>> UpdateAsync(Guid id, Group group);

    Task<IServiceResult> DeleteAsync(Guid id);
    Task<List<User>> GetStudentsByGroupAsync(Guid groupId);
    Task<ServiceResult<BulkGroupImportResultDto>> BulkImportAsync(BulkGroupImportDto dto);
}