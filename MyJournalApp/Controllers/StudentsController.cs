using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Data.Models.Dto;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IGroupRepository _groupRepository;

        public StudentsController(IStudentRepository studentRepository, IGroupRepository groupRepository)
        {
            _studentRepository = studentRepository;
            _groupRepository = groupRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _studentRepository.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();

            var group = await _groupRepository.GetByIdAsync(student.GroupId);
            var dto = new StudentDto
            {
                Id = student.Id,
                FullName = student.FullName,
                Email = student.Email,
                GroupName = group?.Name
            };

            return Ok(dto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Student student)
        {
            if (id != student.Id) return BadRequest();
            await _studentRepository.UpdateAsync(student);
            await _studentRepository.SaveAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _studentRepository.DeleteAsync(id);
            await _studentRepository.SaveAsync();
            return NoContent();
        }
    }
}
