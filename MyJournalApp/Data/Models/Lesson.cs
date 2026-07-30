using MyJournalApp.Data.Models;

public class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;         // Название предмета: "Математика"
    public Guid GroupId { get; set; }                 // Группа, для которой урок
    public Guid TeacherId { get; set; }               // Кто ведет
    public Guid? SecondTeacherId { get; set; }
    public string? Topic { get; set; }       // Тема урока
    public string? Homework { get; set; }             // Домашнее задание
    public DateTime StartTime { get; set; }           // Время начала (дата + время)
    public int? Clocks { get; set; }
    public int? Number { get; set; }
}
