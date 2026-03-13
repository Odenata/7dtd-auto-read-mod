# Bazel workspace for GameApiExporter (7 Days to Die)

workspace(name = "7dtd_auto_read_mod")

load("@bazel_tools//tools/build_defs/repo:local.bzl", "local_repository")
load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

# Local dependency on the shared dev tools repo (recommended layout: sibling checkout).
local_repository(
    name = "7dtd_mod_dev_tools",
    path = "../7dtd-mod-dev-tools",
)

# .NET Framework build rules (required for net472).
http_archive(
    name = "rules_dotnet_framework",
    integrity = "sha256-jZCrri47oPnWxXRpWEeWz+bo7DXdApe/1qz9/KaTdS0=",
    strip_prefix = "rules_dotnet_framework-main",
    url = "https://github.com/balzso/rules_dotnet_framework/archive/refs/heads/main.zip",
)

load("@rules_dotnet_framework//dotnet:defs.bzl", "dotnet_register_toolchains")
dotnet_register_toolchains()

