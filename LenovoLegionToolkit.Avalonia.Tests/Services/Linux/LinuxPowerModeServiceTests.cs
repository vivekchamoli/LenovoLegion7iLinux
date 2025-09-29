using System;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using FluentAssertions;
using LenovoLegionToolkit.Avalonia.Models;
using LenovoLegionToolkit.Avalonia.Services.Interfaces;
using LenovoLegionToolkit.Avalonia.Services;
using LenovoLegionToolkit.Avalonia.Services.Linux;
using Moq;
using Xunit;

namespace LenovoLegionToolkit.Avalonia.Tests.Services.Linux
{
    public class LinuxPowerModeServiceTests
    {
        private readonly MockFileSystem _mockFileSystem;
        private readonly Mock<IProcessRunner> _mockProcessRunner;
        private readonly Mock<IHardwareService> _mockHardwareService;
        private readonly LinuxPowerModeService _service;

        public LinuxPowerModeServiceTests()
        {
            _mockFileSystem = new MockFileSystem();
            _mockProcessRunner = new Mock<IProcessRunner>();
            _mockHardwareService = new Mock<IHardwareService>();
            _service = new LinuxPowerModeService(
                _mockHardwareService.Object,
                new FileSystemService(_mockFileSystem),
                _mockProcessRunner.Object);
        }

        [Fact]
        public async Task GetCurrentPowerModeAsync_ReadsFromLegionLaptopPath()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/power_mode", new MockFileData("1"));

            // Act
            var result = await _service.GetCurrentPowerModeAsync();

            // Assert
            result.Should().Be(PowerMode.Balanced);
        }

        [Fact]
        public async Task GetCurrentPowerModeAsync_FallsBackToPlatformProfile()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/firmware/acpi/platform_profile", new MockFileData("performance"));

            // Act
            var result = await _service.GetCurrentPowerModeAsync();

            // Assert
            result.Should().Be(PowerMode.Performance);
        }

        [Theory]
        [InlineData("quiet", PowerMode.Quiet)]
        [InlineData("low-power", PowerMode.Quiet)]
        [InlineData("balanced", PowerMode.Balanced)]
        [InlineData("performance", PowerMode.Performance)]
        public async Task GetCurrentPowerModeAsync_MapsProfileValueCorrectly(string profileValue, PowerMode expectedMode)
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/firmware/acpi/platform_profile", new MockFileData(profileValue));

            // Act
            var result = await _service.GetCurrentPowerModeAsync();

            // Assert
            result.Should().Be(expectedMode);
        }

        [Fact]
        public async Task SetPowerModeAsync_WritesToLegionLaptopPath()
        {
            // Arrange
            var powerModePath = "/sys/kernel/legion_laptop/power_mode";
            _mockFileSystem.AddFile(powerModePath, new MockFileData("1"));

            // Act
            var result = await _service.SetPowerModeAsync(PowerMode.Performance);

            // Assert
            result.Should().BeTrue();
            var content = _mockFileSystem.GetFile(powerModePath).TextContents;
            content.Should().Be("2"); // Performance mode value
        }

        [Fact]
        public async Task SetPowerModeAsync_FallsBackToPlatformProfile()
        {
            // Arrange
            var profilePath = "/sys/firmware/acpi/platform_profile";
            _mockFileSystem.AddFile(profilePath, new MockFileData("balanced"));

            // Act
            var result = await _service.SetPowerModeAsync(PowerMode.Performance);

            // Assert
            result.Should().BeTrue();
            var content = _mockFileSystem.GetFile(profilePath).TextContents;
            content.Should().Be("performance");
        }

        [Fact]
        public async Task SetPowerModeAsync_RaisesEventOnSuccess()
        {
            // Arrange
            var powerModePath = "/sys/kernel/legion_laptop/power_mode";
            _mockFileSystem.AddFile(powerModePath, new MockFileData("1"));

            PowerMode? capturedMode = null;
            _service.PowerModeChanged += (sender, mode) => capturedMode = mode;

            // Act
            await _service.SetPowerModeAsync(PowerMode.Quiet);

            // Assert
            capturedMode.Should().Be(PowerMode.Quiet);
        }

        [Fact]
        public async Task GetAvailablePowerModesAsync_ReturnsAllModesByDefault()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/power_mode", new MockFileData("1"));

            // Act
            var result = await _service.GetAvailablePowerModesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain(PowerMode.Quiet);
            result.Should().Contain(PowerMode.Balanced);
            result.Should().Contain(PowerMode.Performance);
        }

        [Fact]
        public async Task GetAvailablePowerModesAsync_IncludesCustomWhenAvailable()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/power_mode", new MockFileData("1"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/custom_mode", new MockFileData("1"));

            // Act
            var result = await _service.GetAvailablePowerModesAsync();

            // Assert
            result.Should().Contain(PowerMode.Custom);
        }

        [Fact]
        public async Task GetCustomPowerModeSettingsAsync_ReadsAllSettings()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/custom_mode", new MockFileData("1"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/cpu_pl1", new MockFileData("45"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/cpu_pl2", new MockFileData("65"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/gpu_tgp", new MockFileData("125"));

            // Act
            var result = await _service.GetCustomPowerModeSettingsAsync();

            // Assert
            result.Should().NotBeNull();
            result!.CpuLongTermPowerLimit.Should().Be(45);
            result.CpuShortTermPowerLimit.Should().Be(65);
            result.GpuPowerLimit.Should().Be(125);
        }

        [Fact]
        public async Task SetCustomPowerModeSettingsAsync_WritesAllSettings()
        {
            // Arrange
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/custom_mode", new MockFileData("1"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/cpu_pl1", new MockFileData("0"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/cpu_pl2", new MockFileData("0"));
            _mockFileSystem.AddFile("/sys/kernel/legion_laptop/gpu_tgp", new MockFileData("0"));

            var settings = new CustomPowerMode
            {
                CpuLongTermPowerLimit = 50,
                CpuShortTermPowerLimit = 70,
                GpuPowerLimit = 130
            };

            // Act
            var result = await _service.SetCustomPowerModeSettingsAsync(settings);

            // Assert
            result.Should().BeTrue();
            _mockFileSystem.GetFile("/sys/kernel/legion_laptop/cpu_pl1").TextContents.Should().Be("50");
            _mockFileSystem.GetFile("/sys/kernel/legion_laptop/cpu_pl2").TextContents.Should().Be("70");
            _mockFileSystem.GetFile("/sys/kernel/legion_laptop/gpu_tgp").TextContents.Should().Be("130");
        }

        [Fact]
        public async Task SetCustomPowerModeSettingsAsync_ReturnsFalseWhenNotAvailable()
        {
            // Arrange - No custom_mode file
            var settings = new CustomPowerMode
            {
                CpuLongTermPowerLimit = 50,
                CpuShortTermPowerLimit = 70,
                GpuPowerLimit = 130
            };

            // Act
            var result = await _service.SetCustomPowerModeSettingsAsync(settings);

            // Assert
            result.Should().BeFalse();
        }
    }
}