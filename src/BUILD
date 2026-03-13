load("@rules_dotnet_framework//dotnet:defs.bzl", "net_library")

net_library(
    name = "AutoReadMod",
    srcs = glob(["*.cs"]),
    target_framework = "net472",
    visibility = ["//visibility:public"],
    deps = [
        "@7dtd_mod_dev_tools//third_party/harmony:harmony_net472",
    ],
    data = [
        "ModInfo.xml",
        "Config.xml",
    ],
)
