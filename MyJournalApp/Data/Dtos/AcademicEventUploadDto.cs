using Microsoft.AspNetCore.Http;

public class AcademicEventUploadDto
{
    public IFormFile File { get; set; } = null!;
}