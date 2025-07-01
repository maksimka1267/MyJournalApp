using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseRepository _courseRepository;

        public CoursesController(ICourseRepository courseRepository)
        {
            _courseRepository = courseRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _courseRepository.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            return course == null ? NotFound() : Ok(course);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] Course course)
        {
            await _courseRepository.AddAsync(course);
            await _courseRepository.SaveAsync();
            return CreatedAtAction(nameof(Get), new { id = course.Id }, course);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Course course)
        {
            if (id != course.Id) return BadRequest();
            await _courseRepository.UpdateAsync(course);
            await _courseRepository.SaveAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _courseRepository.DeleteAsync(id);
            await _courseRepository.SaveAsync();
            return NoContent();
        }

        [HttpGet("by-teacher/{teacherId}")]
        public async Task<IActionResult> GetByTeacher(Guid teacherId)
        {
            var courses = await _courseRepository.GetCoursesByTeacherAsync(teacherId);
            return Ok(courses);
        }
    }
}
