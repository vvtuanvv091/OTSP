namespace Backend_Project_OTSP.Dtos;
public class SubjectCardDto
{
    public int Id { get; set; }                    
    public string Name { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-book";
    public int TutorCount { get; set; }
    public List<GradeOptionDto> Grades { get; set; } = new();
}
