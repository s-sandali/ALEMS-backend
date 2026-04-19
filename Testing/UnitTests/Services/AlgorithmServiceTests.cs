using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class AlgorithmServiceTests
{
    private static Algorithm CreateAlgorithm(int id, string name) => new()
    {
        AlgorithmId = id,
        Name = name,
        Category = "Sorting",
        Description = $"{name} description",
        TimeComplexityBest = "O(n)",
        TimeComplexityAverage = "O(n log n)",
        TimeComplexityWorst = "O(n^2)",
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static AlgorithmService BuildSut(Mock<IAlgorithmRepository> repository, IEnumerable<string>? readyAlgorithms = null)
    {
        var settings = (readyAlgorithms ?? [])
            .Select((value, index) => new KeyValuePair<string, string?>($"Quiz:ReadyAlgorithms:{index}", value))
            .ToList();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new AlgorithmService(repository.Object, NullLogger<AlgorithmService>.Instance, configuration);
    }

    [Fact(DisplayName = "GetAllAlgorithmsAsync maps algorithms and marks configured quiz-ready entries")]
    public async Task GetAllAlgorithmsAsync_ReturnsMappedDtos()
    {
        var repository = new Mock<IAlgorithmRepository>();
        repository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                CreateAlgorithm(1, "Bubble Sort"),
                CreateAlgorithm(2, "Merge Sort")
            });

        var result = (await BuildSut(repository, ["bubble_sort"]).GetAllAlgorithmsAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].QuizAvailable.Should().BeTrue();
        result[1].QuizAvailable.Should().BeFalse();
    }

    [Fact(DisplayName = "GetAlgorithmByIdAsync returns null when the algorithm is missing")]
    public async Task GetAlgorithmByIdAsync_Missing_ReturnsNull()
    {
        var repository = new Mock<IAlgorithmRepository>();
        repository
            .Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((Algorithm?)null);

        var result = await BuildSut(repository).GetAlgorithmByIdAsync(99);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "GetAlgorithmByIdAsync maps an algorithm when found")]
    public async Task GetAlgorithmByIdAsync_Found_ReturnsDto()
    {
        var repository = new Mock<IAlgorithmRepository>();
        repository
            .Setup(r => r.GetByIdAsync(5))
            .ReturnsAsync(CreateAlgorithm(5, "Heap Sort"));

        var result = await BuildSut(repository, ["heap_sort"]).GetAlgorithmByIdAsync(5);

        result.Should().NotBeNull();
        result!.AlgorithmId.Should().Be(5);
        result.QuizAvailable.Should().BeTrue();
    }
}
