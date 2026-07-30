using MyJournalApp.Result;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service
{
    public class JournalService : IJournalService
    {
        private readonly IJournalEntryRepository _journalRepository;
        private readonly IGradeRepository _gradeRepository;
        private readonly ITeacherRepository _teacherRepository;

        public JournalService(
            IJournalEntryRepository journalRepository,
            IGradeRepository gradeRepository,
            ITeacherRepository teacherRepository)
        {
            _journalRepository = journalRepository;
            _gradeRepository = gradeRepository;
            _teacherRepository = teacherRepository;
        }
        public async Task<IEnumerable<JournalEntry>> GetAllAsync()
        {
            return await _journalRepository.GetAllAsync();
        }
        public async Task<IEnumerable<JournalEntry>> GetTeacherJournalsAsync(Guid teacherId)
        {
            return await _journalRepository.GetByTeacherIdAsync(teacherId);
        }
        public async Task<JournalEntry?> GetByIdAsync(Guid id)
        {
            return await _journalRepository.GetByIdAsync(id);
        }
        public async Task<ServiceResult<JournalEntry>> CreateAsync(JournalEntry journal)
        {
            journal.Id = Guid.NewGuid();

            await _journalRepository.AddAsync(journal);

            return ServiceResult<JournalEntry>.Ok(journal);
        }
        public async Task<bool> IsDirectorAsync(Guid teacherId)
        {
            var teacher = await _teacherRepository.GetByIdAsync(teacherId);

            return teacher?.IsDirector == true;
        }
        public async Task<ServiceResult<JournalEntry>> UpdateAsync(Guid id, JournalEntry journal)
        {
            var existing = await _journalRepository.GetByIdAsync(id);

            if (existing == null)
                return ServiceResult<JournalEntry>.Fail("Journal not found");

            existing.Name = journal.Name;
            existing.Date = journal.Date;
            existing.Comment = journal.Comment;
            existing.GroupId = journal.GroupId;
            existing.TeacherId = journal.TeacherId;

            await _journalRepository.Update(existing);

            return ServiceResult<JournalEntry>.Ok(existing);
        }
        public async Task<IServiceResult> DeleteAsync(Guid id)
        {
            var existing = await _journalRepository.GetByIdAsync(id);

            if (existing == null)
                return IServiceResult.Fail("Journal not found");

            await _gradeRepository.DeleteByJournalEntryIdAsync(id);

            await _journalRepository.Delete(existing);

            return IServiceResult.Ok();
        }

    }
}
