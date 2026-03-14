load("@rules_dotnet_framework//dotnet:defs.bzl", "net_library")

net_library(
    name = "AutoReadMod",
    srcs = [
        "ModApi.cs",
        "ModConfig.cs",
        "ContainerOpenPatch.cs",
        "AutoReader.cs",
        "ApiExperiments.cs",
    ],
    target_framework = "net472",
    visibility = ["//visibility:public"],
    deps = [
        "@seven_days_to_die_mod_dev_tools//third_party/harmony:harmony_net472",
    ],
    data = [
        "ModInfo.xml",
        "Config.xml",
    ],
)
