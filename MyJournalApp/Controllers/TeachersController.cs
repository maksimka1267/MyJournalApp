using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeachersController : ControllerBase
    {
        private readonly ITeacherRepository _teacherRepository;

        public TeachersController(ITeacherRepository teacherRepository)
        {
            _teacherRepository = teacherRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _teacherRepository.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var teacher = await _teacherRepository.GetByIdAsync(id);
            return teacher == null ? NotFound() : Ok(teacher);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Teacher teacher)
        {
            if (id != teacher.Id) return BadRequest();
            await _teacherRepository.UpdateAsync(teacher);
            await _teacherRepository.SaveAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _teacherRepository.DeleteAsync(id);
            await _teacherRepository.SaveAsync();
            return NoContent();
        }
    }
}
