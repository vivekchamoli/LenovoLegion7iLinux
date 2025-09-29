#!/usr/bin/env python3
"""
Legion Toolkit Test Runner
Automated test execution with comprehensive reporting
"""

import sys
import os
import subprocess
import argparse
from pathlib import Path
import json
from datetime import datetime
import platform


class LegionTestRunner:
    """Test runner for Legion Toolkit with comprehensive reporting"""

    def __init__(self):
        self.script_dir = Path(__file__).parent
        self.test_dir = self.script_dir / "tests"
        self.reports_dir = self.script_dir / "test_reports"
        self.reports_dir.mkdir(exist_ok=True)

    def check_test_environment(self) -> dict:
        """Check test environment and requirements"""
        env_info = {
            "platform": platform.system().lower(),
            "python_version": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
            "pytest_available": False,
            "hardware_available": False,
            "root_privileges": os.geteuid() == 0 if hasattr(os, 'geteuid') else False,
            "kernel_module_loaded": False,
            "nvidia_gpu": False,
            "legion_hardware": False
        }

        # Check pytest availability
        try:
            import pytest
            env_info["pytest_available"] = True
        except ImportError:
            print("❌ Pytest not installed. Run: pip install pytest pytest-asyncio pytest-cov")
            return env_info

        # Check for Legion hardware
        if env_info["platform"] == "linux":
            try:
                with open("/sys/class/dmi/id/product_name", "r") as f:
                    product_name = f.read().strip()
                    if "legion" in product_name.lower():
                        env_info["legion_hardware"] = True
            except:
                pass

            # Check kernel module
            try:
                result = subprocess.run(["lsmod"], capture_output=True, text=True)
                if "legion_laptop_16irx9" in result.stdout:
                    env_info["kernel_module_loaded"] = True
            except:
                pass

            # Check for NVIDIA GPU
            try:
                result = subprocess.run(["lspci"], capture_output=True, text=True)
                if "NVIDIA" in result.stdout:
                    env_info["nvidia_gpu"] = True
            except:
                pass

        # Set hardware availability
        env_info["hardware_available"] = (
            env_info["legion_hardware"] and
            env_info["kernel_module_loaded"]
        )

        return env_info

    def print_environment_info(self, env_info: dict):
        """Print environment information"""
        print("🔍 Test Environment Information")
        print("=" * 50)
        print(f"Platform: {env_info['platform']}")
        print(f"Python Version: {env_info['python_version']}")
        print(f"Pytest Available: {'✅' if env_info['pytest_available'] else '❌'}")
        print(f"Legion Hardware: {'✅' if env_info['legion_hardware'] else '❌'}")
        print(f"Kernel Module: {'✅' if env_info['kernel_module_loaded'] else '❌'}")
        print(f"NVIDIA GPU: {'✅' if env_info['nvidia_gpu'] else '❌'}")
        print(f"Root Privileges: {'✅' if env_info['root_privileges'] else '❌'}")
        print(f"Hardware Tests: {'✅ Enabled' if env_info['hardware_available'] else '⚠️ Simulated'}")
        print()

    def run_unit_tests(self, verbose: bool = False) -> tuple:
        """Run unit tests"""
        print("🧪 Running Unit Tests...")

        cmd = [
            sys.executable, "-m", "pytest",
            str(self.test_dir),
            "-m", "not integration and not hardware and not slow",
            "--tb=short",
            "--disable-warnings"
        ]

        if verbose:
            cmd.append("-v")

        # Add coverage if available
        try:
            import pytest_cov
            cmd.extend([
                "--cov=legion_toolkit",
                "--cov-report=html:" + str(self.reports_dir / "coverage"),
                "--cov-report=term-missing"
            ])
        except ImportError:
            print("ℹ️ pytest-cov not available, skipping coverage report")

        result = subprocess.run(cmd, capture_output=True, text=True)
        return result.returncode, result.stdout, result.stderr

    def run_integration_tests(self, verbose: bool = False) -> tuple:
        """Run integration tests"""
        print("🔗 Running Integration Tests...")

        cmd = [
            sys.executable, "-m", "pytest",
            str(self.test_dir),
            "-m", "integration",
            "--tb=short",
            "--disable-warnings"
        ]

        if verbose:
            cmd.append("-v")

        result = subprocess.run(cmd, capture_output=True, text=True)
        return result.returncode, result.stdout, result.stderr

    def run_hardware_tests(self, force: bool = False, verbose: bool = False) -> tuple:
        """Run hardware tests"""
        print("🔧 Running Hardware Tests...")

        cmd = [
            sys.executable, "-m", "pytest",
            str(self.test_dir),
            "-m", "hardware",
            "--tb=short",
            "--disable-warnings"
        ]

        if force:
            # Force hardware tests even without actual hardware
            cmd.extend(["-s", "--capture=no"])
            os.environ["LEGION_FORCE_TESTS"] = "1"

        if verbose:
            cmd.append("-v")

        result = subprocess.run(cmd, capture_output=True, text=True)
        return result.returncode, result.stdout, result.stderr

    def run_performance_tests(self, verbose: bool = False) -> tuple:
        """Run performance/slow tests"""
        print("⚡ Running Performance Tests...")

        cmd = [
            sys.executable, "-m", "pytest",
            str(self.test_dir),
            "-m", "slow",
            "--tb=short",
            "--disable-warnings"
        ]

        if verbose:
            cmd.append("-v")

        result = subprocess.run(cmd, capture_output=True, text=True)
        return result.returncode, result.stdout, result.stderr

    def run_all_tests(self, include_hardware: bool = False, force_hardware: bool = False, verbose: bool = False) -> dict:
        """Run all test suites"""
        results = {}
        total_passed = 0
        total_failed = 0

        print("🚀 Starting Comprehensive Test Suite")
        print("=" * 60)

        # Unit tests (always run)
        returncode, stdout, stderr = self.run_unit_tests(verbose)
        results["unit"] = {
            "returncode": returncode,
            "stdout": stdout,
            "stderr": stderr,
            "passed": returncode == 0
        }
        if returncode == 0:
            total_passed += 1
        else:
            total_failed += 1

        # Integration tests
        returncode, stdout, stderr = self.run_integration_tests(verbose)
        results["integration"] = {
            "returncode": returncode,
            "stdout": stdout,
            "stderr": stderr,
            "passed": returncode == 0
        }
        if returncode == 0:
            total_passed += 1
        else:
            total_failed += 1

        # Hardware tests (conditional)
        if include_hardware or force_hardware:
            returncode, stdout, stderr = self.run_hardware_tests(force_hardware, verbose)
            results["hardware"] = {
                "returncode": returncode,
                "stdout": stdout,
                "stderr": stderr,
                "passed": returncode == 0
            }
            if returncode == 0:
                total_passed += 1
            else:
                total_failed += 1

        # Performance tests
        returncode, stdout, stderr = self.run_performance_tests(verbose)
        results["performance"] = {
            "returncode": returncode,
            "stdout": stdout,
            "stderr": stderr,
            "passed": returncode == 0
        }
        if returncode == 0:
            total_passed += 1
        else:
            total_failed += 1

        results["summary"] = {
            "total_suites": len(results) - 1,  # Exclude summary
            "passed_suites": total_passed,
            "failed_suites": total_failed,
            "success_rate": (total_passed / (total_passed + total_failed)) * 100 if (total_passed + total_failed) > 0 else 0
        }

        return results

    def generate_report(self, results: dict, env_info: dict):
        """Generate comprehensive test report"""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        report_file = self.reports_dir / f"test_report_{timestamp}.json"

        report = {
            "timestamp": datetime.now().isoformat(),
            "environment": env_info,
            "results": results,
            "summary": results.get("summary", {}),
            "recommendations": self._generate_recommendations(results, env_info)
        }

        # Save JSON report
        with open(report_file, 'w') as f:
            json.dump(report, f, indent=2)

        # Generate HTML report
        html_file = self.reports_dir / f"test_report_{timestamp}.html"
        self._generate_html_report(report, html_file)

        return report_file, html_file

    def _generate_recommendations(self, results: dict, env_info: dict) -> list:
        """Generate recommendations based on test results"""
        recommendations = []

        # Environment recommendations
        if not env_info["pytest_available"]:
            recommendations.append("Install pytest: pip install pytest pytest-asyncio pytest-cov")

        if not env_info["legion_hardware"]:
            recommendations.append("Tests running on non-Legion hardware - results may be limited")

        if not env_info["kernel_module_loaded"] and env_info["platform"] == "linux":
            recommendations.append("Install kernel module for full hardware support")

        if not env_info["root_privileges"] and env_info["platform"] == "linux":
            recommendations.append("Run with sudo for hardware tests: sudo python run_tests.py --hardware")

        # Test result recommendations
        for suite_name, suite_results in results.items():
            if suite_name == "summary":
                continue

            if not suite_results["passed"]:
                recommendations.append(f"Fix failing {suite_name} tests - check detailed output")

        # Performance recommendations
        if env_info["platform"] == "linux" and not env_info["nvidia_gpu"]:
            recommendations.append("Install NVIDIA drivers for GPU-related tests")

        return recommendations

    def _generate_html_report(self, report: dict, html_file: Path):
        """Generate HTML test report"""
        html_content = f"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Legion Toolkit Test Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #2c3e50; margin-bottom: 10px; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 30px; }}
        .metric-card {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; text-align: center; }}
        .metric-value {{ font-size: 2em; font-weight: bold; }}
        .metric-label {{ font-size: 0.9em; opacity: 0.9; }}
        .section {{ margin-bottom: 30px; }}
        .section h2 {{ color: #34495e; border-bottom: 2px solid #3498db; padding-bottom: 10px; }}
        .test-suite {{ margin: 15px 0; padding: 15px; border-left: 4px solid #ddd; background: #f9f9f9; }}
        .test-suite.passed {{ border-left-color: #27ae60; background: #d5f4e6; }}
        .test-suite.failed {{ border-left-color: #e74c3c; background: #fdeaea; }}
        .env-info {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 15px; }}
        .env-item {{ padding: 10px; background: #ecf0f1; border-radius: 5px; }}
        .recommendations {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; }}
        .recommendations ul {{ margin: 0; padding-left: 20px; }}
        .status {{ font-weight: bold; padding: 3px 8px; border-radius: 3px; font-size: 0.8em; }}
        .status.passed {{ background: #d4edda; color: #155724; }}
        .status.failed {{ background: #f8d7da; color: #721c24; }}
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🚀 Legion Toolkit Test Report</h1>
            <p>Generated on {report['timestamp']}</p>
        </div>

        <div class="summary">
            <div class="metric-card">
                <div class="metric-value">{report['summary']['total_suites']}</div>
                <div class="metric-label">Test Suites</div>
            </div>
            <div class="metric-card">
                <div class="metric-value">{report['summary']['passed_suites']}</div>
                <div class="metric-label">Passed</div>
            </div>
            <div class="metric-card">
                <div class="metric-value">{report['summary']['failed_suites']}</div>
                <div class="metric-label">Failed</div>
            </div>
            <div class="metric-card">
                <div class="metric-value">{report['summary']['success_rate']:.1f}%</div>
                <div class="metric-label">Success Rate</div>
            </div>
        </div>

        <div class="section">
            <h2>🔍 Environment Information</h2>
            <div class="env-info">
                <div class="env-item">
                    <strong>Platform:</strong> {report['environment']['platform']}
                </div>
                <div class="env-item">
                    <strong>Python:</strong> {report['environment']['python_version']}
                </div>
                <div class="env-item">
                    <strong>Legion Hardware:</strong> {'✅' if report['environment']['legion_hardware'] else '❌'}
                </div>
                <div class="env-item">
                    <strong>Kernel Module:</strong> {'✅' if report['environment']['kernel_module_loaded'] else '❌'}
                </div>
                <div class="env-item">
                    <strong>NVIDIA GPU:</strong> {'✅' if report['environment']['nvidia_gpu'] else '❌'}
                </div>
                <div class="env-item">
                    <strong>Root Access:</strong> {'✅' if report['environment']['root_privileges'] else '❌'}
                </div>
            </div>
        </div>

        <div class="section">
            <h2>📊 Test Results</h2>
"""

        # Add test suite results
        for suite_name, suite_data in report['results'].items():
            if suite_name == "summary":
                continue

            status_class = "passed" if suite_data["passed"] else "failed"
            status_text = "✅ PASSED" if suite_data["passed"] else "❌ FAILED"

            html_content += f"""
            <div class="test-suite {status_class}">
                <h3>{suite_name.title()} Tests <span class="status {status_class}">{status_text}</span></h3>
                <p><strong>Return Code:</strong> {suite_data['returncode']}</p>
                <details>
                    <summary>Output</summary>
                    <pre>{suite_data['stdout']}</pre>
                    {f'<pre style="color: red;">{suite_data["stderr"]}</pre>' if suite_data['stderr'] else ''}
                </details>
            </div>
"""

        # Add recommendations
        if report['recommendations']:
            html_content += """
        </div>

        <div class="section">
            <h2>💡 Recommendations</h2>
            <div class="recommendations">
                <ul>
"""
            for rec in report['recommendations']:
                html_content += f"<li>{rec}</li>"

            html_content += """
                </ul>
            </div>
        </div>
"""

        html_content += """
    </div>
</body>
</html>
"""

        with open(html_file, 'w') as f:
            f.write(html_content)

    def print_summary(self, results: dict):
        """Print test summary"""
        summary = results.get("summary", {})

        print("\n" + "=" * 60)
        print("📊 TEST SUMMARY")
        print("=" * 60)
        print(f"Total Test Suites: {summary.get('total_suites', 0)}")
        print(f"Passed: {summary.get('passed_suites', 0)}")
        print(f"Failed: {summary.get('failed_suites', 0)}")
        print(f"Success Rate: {summary.get('success_rate', 0):.1f}%")

        print("\n📋 DETAILED RESULTS:")
        for suite_name, suite_data in results.items():
            if suite_name == "summary":
                continue

            status = "✅ PASSED" if suite_data["passed"] else "❌ FAILED"
            print(f"  {suite_name.ljust(15)}: {status}")

        print("=" * 60)


def main():
    """Main test runner entry point"""
    parser = argparse.ArgumentParser(description="Legion Toolkit Test Runner")
    parser.add_argument("--unit", action="store_true", help="Run only unit tests")
    parser.add_argument("--integration", action="store_true", help="Run only integration tests")
    parser.add_argument("--hardware", action="store_true", help="Include hardware tests")
    parser.add_argument("--performance", action="store_true", help="Run only performance tests")
    parser.add_argument("--force-hardware", action="store_true", help="Force hardware tests even without hardware")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--report", action="store_true", help="Generate detailed report")

    args = parser.parse_args()

    runner = LegionTestRunner()

    # Check environment
    env_info = runner.check_test_environment()
    runner.print_environment_info(env_info)

    if not env_info["pytest_available"]:
        print("❌ Cannot run tests without pytest. Install with: pip install pytest")
        sys.exit(1)

    # Run specific test suites
    if args.unit:
        returncode, stdout, stderr = runner.run_unit_tests(args.verbose)
        print(stdout)
        if stderr:
            print("STDERR:", stderr)
        sys.exit(returncode)

    elif args.integration:
        returncode, stdout, stderr = runner.run_integration_tests(args.verbose)
        print(stdout)
        if stderr:
            print("STDERR:", stderr)
        sys.exit(returncode)

    elif args.performance:
        returncode, stdout, stderr = runner.run_performance_tests(args.verbose)
        print(stdout)
        if stderr:
            print("STDERR:", stderr)
        sys.exit(returncode)

    else:
        # Run all tests
        results = runner.run_all_tests(
            include_hardware=args.hardware or env_info["hardware_available"],
            force_hardware=args.force_hardware,
            verbose=args.verbose
        )

        runner.print_summary(results)

        # Generate report if requested
        if args.report:
            report_file, html_file = runner.generate_report(results, env_info)
            print(f"\n📄 Reports generated:")
            print(f"  JSON: {report_file}")
            print(f"  HTML: {html_file}")

        # Exit with appropriate code
        if results["summary"]["failed_suites"] > 0:
            sys.exit(1)
        else:
            print("\n🎉 All tests passed!")
            sys.exit(0)


if __name__ == "__main__":
    main()