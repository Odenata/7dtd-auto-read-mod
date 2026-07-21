load("@rules_dotnet_framework//dotnet:defs.bzl", "net_xunit_test")

UNIT_TESTS = [
    ":auto_reader",
    ":mod_api",
    ":mod_config",
    ":item_identification",
    ":configuration_filtering",
    ":debug_logging",
]

TOOL_TESTS = [
    ":build_tool",
    ":deploy_tool",
]

INTEGRATION_TESTS = [
    ":game_launch",
]

ALL_TESTS = UNIT_TESTS + TOOL_TESTS

XUNIT_DEPS = [
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.collections.concurrent.dll",
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.collections.dll",
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.objectmodel.dll",
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.runtime.dll",
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.runtime.extensions.dll",
    "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.threading.tasks.dll",
    "@xunit_assert//:net",
    "@xunit_extensibility_core//:net",
    "@xunit_extensibility_execution//:net",
]

MOD_TEST_DEPS = XUNIT_DEPS + [
    "//src:AutoReadMod",
]

net_xunit_test(
    name = "auto_reader",
    size = "small",
    srcs = [
        "AssemblyInfo.cs",
        "AutoReaderTests.cs",
        "AutoReaderBehaviorTests.cs",
        "BundleOpenPatchTests.cs",
        "MagazineBonusAndSkipAlreadyReadTests.cs",
        "BookSetAndSandboxMagazineTests.cs",
        "InventoryAddModeTests.cs",
        "ReadableEffectMessageBuilderTests.cs",
        "VisualizationTests.cs",
    ],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "mod_api",
    size = "small",
    srcs = ["ModApiTests.cs"],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "mod_config",
    size = "small",
    srcs = ["ModConfigTests.cs"],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "item_identification",
    size = "small",
    srcs = ["ItemIdentificationTests.cs"],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "configuration_filtering",
    size = "small",
    srcs = ["ConfigurationFilteringTests.cs"],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "debug_logging",
    size = "small",
    srcs = ["DebugLoggingTests.cs"],
    deps = MOD_TEST_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "build_tool",
    size = "small",
    srcs = [
        "BuildToolTests.cs",
        "TestDataPath.cs",
    ],
    data = ["//tools:build.ps1"],
    deps = XUNIT_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "deploy_tool",
    size = "small",
    srcs = [
        "DeployToolTests.cs",
        "TestDataPath.cs",
    ],
    data = ["//tools:deploy.ps1"],
    deps = XUNIT_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
)

net_xunit_test(
    name = "game_launch",
    size = "medium",
    srcs = ["GameLaunchTests.cs"],
    deps = XUNIT_DEPS,
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    testlauncher = "@xunit_runner_console//:net",
    tags = [
        "local",
        "manual",
    ],
)

test_suite(
    name = "unit",
    tests = UNIT_TESTS,
)

test_suite(
    name = "tools",
    tests = TOOL_TESTS,
)

test_suite(
    name = "integration",
    tests = INTEGRATION_TESTS,
)

test_suite(
    name = "all",
    tests = ALL_TESTS,
)
