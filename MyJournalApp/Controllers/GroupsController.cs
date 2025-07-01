using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Data.Models.Dto;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IStudentRepository _studentRepository;

        public GroupsController(IGroupRepository groupRepository, IStudentRepository studentRepository)
        {
            _groupRepository = groupRepository;
            _studentRepository = studentRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _groupRepository.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            return group == null ? NotFound() : Ok(group);
        }

        [HttpGet("{id}/students")]
        public async Task<IActionResult> GetWithStudents(Guid id)
        {
            var group = await _groupRepository.GetGroupWithStudentsAsync(id);
            return group == null ? NotFound() : Ok(group);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm]Group group)
        {
            await _groupRepository.AddAsync(group);
            foreach(var items in group.Students)
            {
                var student = await _studentRepository.GetByIdAsync(items);
                if(student == null)
                {
                    return BadRequest("Invalid Students ID");
                }
                else
                {
                    student.GroupId = group.Id;
                    await _studentRepository.UpdateAsync(student);
                }
            }
            await _groupRepository.SaveAsync();
            return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, Group group)
        {
            if (id != group.Id) return BadRequest();
            await _groupRepository.UpdateAsync(group);
            await _groupRepository.SaveAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _groupRepository.DeleteAsync(id);
            await _groupRepository.SaveAsync();
            return NoContent();
        }
    }
}
