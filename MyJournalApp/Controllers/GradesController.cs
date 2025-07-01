using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GradesController : ControllerBase
    {
        private readonly IGradeRepository _gradeRepository;

        public GradesController(IGradeRepository gradeRepository)
        {
            _gradeRepository = gradeRepository;
        }

        [HttpGet("student/{studentId}")]
        public async Task<IActionResult> GetByStudent(Guid studentId)
        {
            var grades = await _gradeRepository.GetGradesByStudentAsync(studentId);
            return Ok(grades);
        }

        [HttpPost]
        public async Task<IActionResult> AddGrade([FromForm] Grade grade)
        {
            await _gradeRepository.AddAsync(grade);
            await _gradeRepository.SaveAsync();
            return Ok(grade);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Grade updated)
        {
            if (id != updated.Id) return BadRequest();
            await _gradeRepository.UpdateAsync(updated);
            await _gradeRepository.SaveAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _gradeRepository.DeleteAsync(id);
            await _gradeRepository.SaveAsync();
            return NoContent();
        }
    }
}
