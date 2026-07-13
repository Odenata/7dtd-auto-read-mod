load("@rules_dotnet_framework//dotnet:defs.bzl", "net_library")

net_library(
    name = "AutoReadMod",
    out = "AutoReadMod.dll",
    srcs = [
        "AssemblyInfo.cs",
        "ModApi.cs",
        "ModConfig.cs",
        "ContainerOpenPatch.cs",
        "BagOpenPatch.cs",
        "BundleOpenPatch.cs",
        "GameReflection.cs",
        "AutoReader.cs",
        "AutoReadVisualizationEvent.cs",
        "AutoReadVisualizationQueue.cs",
        "AutoReadVisualizationOverlay.cs",
        "GearsModApi.cs",
    ],
    target_framework = "net472",
    dotnet_context_data = "@rules_dotnet_framework//:net_context_data_net472",
    visibility = ["//visibility:public"],
    deps = [
        "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.xml.dll",
        "@rules_dotnet_framework//dotnet/stdlib.net/net472:system.runtime.dll",
        "@seven_days_to_die_mod_dev_tools//mocks:Assembly-CSharp.dll",
        "@seven_days_to_die_mod_dev_tools//mocks:UnityEngine.CoreModule.dll",
        "@seven_days_to_die_mod_dev_tools//third_party/harmony:harmony_net472_import",
        "//third_party/gearsapi:gearsapi_net472_import",
        "//third_party/gearsapi:incontrol_net472_import",
    ],
    data = [
        "ModInfo.xml",
        "Config.xml",
        "ModSettings.xml",
    ],
)
