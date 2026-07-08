namespace PiddeCorp.TaskManager.API.Models;

public class OpenTaskDto {
    public Guid Id { get; init; }
    public DateTime Created { get; init; }
    public int Type { get; init; }
    public string TypeName { get; set; } = string.Empty;
    public DateTime Due { get; init; }
    public string WorkflowStatus { get; init; } = string.Empty;
    public string? AssignedTo { get; init; }
}
