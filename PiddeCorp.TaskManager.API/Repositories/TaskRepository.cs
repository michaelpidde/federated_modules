using PiddeCorp.TaskManager.API.Models;

namespace PiddeCorp.TaskManager.API.Repositories;

public class TaskRepository(IConfiguration configuration) {
    public IEnumerable<OpenTaskDto> GetOpenTasks() {
        List<OpenTaskDto> tasks = [
            new() {
                Id = Guid.NewGuid(),
                Created = DateTime.Now,
                TypeName = "Task",
                Due = DateTime.Now.AddDays(3),
                WorkflowStatus = "New",
                AssignedTo = "",
            },
            new() {
                Id = Guid.NewGuid(),
                Created = DateTime.Now.AddDays(3),
                TypeName = "Task",
                Due = DateTime.Now.AddDays(4),
                WorkflowStatus = "New",
                AssignedTo = "",
            },
            new() {
                Id = Guid.NewGuid(),
                Created = DateTime.Now.AddDays(5),
                TypeName = "Task",
                Due = DateTime.Now.AddDays(7),
                WorkflowStatus = "New",
                AssignedTo = "",
            },
        ];
        
        return tasks;
    }
}
