sh_test(
    name = "dotnet_unit_tests",
    srcs = ["run-dotnet-tests.ps1"],
    args = [],
    data = glob(["**/*"]),
    tags = ["local"],
)

test_suite(
    name = "all",
    tests = [
        ":dotnet_unit_tests",
    ],
)
