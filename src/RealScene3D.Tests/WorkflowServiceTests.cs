using Microsoft.Extensions.Logging;
using Moq;
using RealScene3D.Application.DTOs;
using RealScene3D.Application.Services;
using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using RealScene3D.Infrastructure.Repositories;

namespace RealScene3D.Tests;

/// <summary>
/// 工作流服务单元测试
/// </summary>
public class WorkflowServiceTests
{
    private readonly Mock<IRepository<Workflow>> _workflowRepositoryMock;
    private readonly Mock<IRepository<WorkflowInstance>> _workflowInstanceRepositoryMock;
    private readonly Mock<IRepository<WorkflowExecutionHistory>> _executionHistoryRepositoryMock;
    private readonly Mock<IWorkflowEngine> _workflowEngineMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ApplicationDbContext> _contextMock;
    private readonly Mock<ILogger<WorkflowService>> _loggerMock;

    private readonly WorkflowService _workflowService;

    public WorkflowServiceTests()
    {
        _workflowRepositoryMock = new Mock<IRepository<Workflow>>();
        _workflowInstanceRepositoryMock = new Mock<IRepository<WorkflowInstance>>();
        _executionHistoryRepositoryMock = new Mock<IRepository<WorkflowExecutionHistory>>();
        _workflowEngineMock = new Mock<IWorkflowEngine>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _contextMock = new Mock<ApplicationDbContext>();
        _loggerMock = new Mock<ILogger<WorkflowService>>();

        _workflowService = new WorkflowService(
            _workflowRepositoryMock.Object,
            _workflowInstanceRepositoryMock.Object,
            _executionHistoryRepositoryMock.Object,
            _workflowEngineMock.Object,
            _unitOfWorkMock.Object,
            _contextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateWorkflowAsync_ValidRequest_ReturnsWorkflowDto()
    {
        // Arrange
        var request = new CreateWorkflowRequest
        {
            Name = "测试工作流",
            Description = "测试描述",
            Definition = "{\"nodes\":[],\"connections\":[]}",
            Version = "1.0.0"
        };

        var userId = Guid.NewGuid();
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Definition = request.Definition,
            Version = request.Version,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _workflowRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Workflow>()))
            .ReturnsAsync(workflow);

        // Act
        var result = await _workflowService.CreateWorkflowAsync(request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.Equal(workflow.Name, result.Name);
        Assert.Equal(workflow.Description, result.Description);

        _workflowRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Workflow>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateWorkflowAsync_InvalidDefinition_ThrowsException()
    {
        // Arrange
        var request = new CreateWorkflowRequest
        {
            Name = "测试工作流",
            Definition = "无效的JSON"
        };

        var userId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflowService.CreateWorkflowAsync(request, userId));
    }

    [Fact]
    public async Task StartWorkflowInstanceAsync_ValidWorkflow_ReturnsInstanceDto()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var workflow = new Workflow
        {
            Id = workflowId,
            Name = "测试工作流",
            IsEnabled = true
        };

        var request = new StartWorkflowInstanceRequest
        {
            Name = "测试实例",
            InputParameters = "{}"
        };

        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Name = request.Name,
            Status = WorkflowInstanceStatus.Created,
            CreatedBy = userId
        };

        _workflowRepositoryMock.Setup(r => r.GetByIdAsync(workflowId))
            .ReturnsAsync(workflow);
        _workflowInstanceRepositoryMock.Setup(r => r.AddAsync(It.IsAny<WorkflowInstance>()))
            .ReturnsAsync(instance);

        // Act
        var result = await _workflowService.StartWorkflowInstanceAsync(workflowId, request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(instance.Id, result.Id);
        Assert.Equal(instance.Name, result.Name);

        _workflowInstanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<WorkflowInstance>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetWorkflowByIdAsync_ExistingWorkflow_ReturnsWorkflowDto()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new Workflow
        {
            Id = workflowId,
            Name = "测试工作流",
            Description = "测试描述",
            IsEnabled = true
        };

        _workflowRepositoryMock.Setup(r => r.GetByIdAsync(workflowId))
            .ReturnsAsync(workflow);

        // Act
        var result = await _workflowService.GetWorkflowByIdAsync(workflowId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflow.Id, result.Id);
        Assert.Equal(workflow.Name, result.Name);
        Assert.Equal(workflow.Description, result.Description);

        _workflowRepositoryMock.Verify(r => r.GetByIdAsync(workflowId), Times.Once);
    }

    [Fact]
    public async Task GetWorkflowByIdAsync_NonExistingWorkflow_ReturnsNull()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        _workflowRepositoryMock.Setup(r => r.GetByIdAsync(workflowId))
            .ReturnsAsync((Workflow?)null);

        // Act
        var result = await _workflowService.GetWorkflowByIdAsync(workflowId);

        // Assert
        Assert.Null(result);
        _workflowRepositoryMock.Verify(r => r.GetByIdAsync(workflowId), Times.Once);
    }

    [Fact]
    public async Task CancelWorkflowInstanceAsync_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var instance = new WorkflowInstance
        {
            Id = instanceId,
            Status = WorkflowInstanceStatus.Running,
            CreatedBy = userId
        };

        _workflowInstanceRepositoryMock.Setup(r => r.GetByIdAsync(instanceId))
            .ReturnsAsync(instance);

        // Act
        var result = await _workflowService.CancelWorkflowInstanceAsync(instanceId, userId);

        // Assert
        Assert.True(result);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);

        _workflowInstanceRepositoryMock.Verify(r => r.GetByIdAsync(instanceId), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelWorkflowInstanceAsync_UnauthorizedUser_ReturnsFalse()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var instance = new WorkflowInstance
        {
            Id = instanceId,
            Status = WorkflowInstanceStatus.Running,
            CreatedBy = userId
        };

        _workflowInstanceRepositoryMock.Setup(r => r.GetByIdAsync(instanceId))
            .ReturnsAsync(instance);

        // Act
        var result = await _workflowService.CancelWorkflowInstanceAsync(instanceId, differentUserId);

        // Assert
        Assert.False(result);
        _workflowInstanceRepositoryMock.Verify(r => r.GetByIdAsync(instanceId), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}