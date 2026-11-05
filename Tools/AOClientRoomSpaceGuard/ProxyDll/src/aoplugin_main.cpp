// The client patch as an AOPlugins plugin (https://github.com/McAndersDK/aoplugins, include/aop_plugin.h): the same repairs as the
// version.dll build, loaded by AOPlugins' version.dll from the client's AOPlugins\ folder. AOPlugins forwards the real
// version.dll, so version_proxy.cpp isn't part of this build, and DllMain does nothing (dllmain.cpp, AO_REBIRTH_AOPLUGIN).

#include "aop_plugin.h"
#include "build_info.h"

namespace aorf
{
    void StartClientPatch();
}

namespace
{
    const AopPluginInfo Info = {
        sizeof(AopPluginInfo), AOP_API_VERSION,
        "aorebirth-patch", "AORebirth Client Patch", AOP_VERSION(2, 0, 0),
        0, // a utility: crash repairs and login routing load alongside any project
        nullptr, 0,
    };

    const AopPlugin Plugin = {
        AOP_API_VERSION, sizeof(AopPlugin),
        nullptr, nullptr, nullptr, nullptr, nullptr, nullptr,
    };

    // The launcher reads AORebirth's launcher pages and dimensions: Build-Plugin.cmd ships these files in
    // AOPlugins\aorebirth-patch\, and AOPlugins hands them to the launcher in place of the game's (which stay as
    // they are). This replaces the installer's overwriting of AnarchyLauncher.url / DimensionServer.url.
    // dimensions.txt is the launcher's local copy of the dimension list: it downloads the list from the server in
    // DimensionServer.url, writes it there and reads it back - and reads ours, so the dropdown shows the local list.
    const AopLauncherFile LauncherFiles[] = {
        { "AnarchyLauncher.url", "aorebirth-patch\\AnarchyLauncher.url" },
        { "DimensionServer.url", "aorebirth-patch\\DimensionServer.url" },
        { "dimensions.txt", "aorebirth-patch\\dimensions.txt" },
    };

    const AopLauncherInfo Launcher = {
        sizeof(AopLauncherInfo), LauncherFiles, 3,
    };
}

AOP_EXPORT const AopPluginInfo* __cdecl AopPluginQuery(void)
{
    return &Info;
}

AOP_EXPORT const AopLauncherInfo* __cdecl AopLauncherQuery(void)
{
    return &Launcher;
}

AOP_EXPORT const AopPlugin* __cdecl AopPluginAttach(const AopHost* host)
{
    host->log(
        host,
        "version %s source %s - log in %%LOCALAPPDATA%%\\AORebirthClientPatch",
        aorf::ClientPatchVersion,
        aorf::ClientPatchSourceSha);
    // Its own worker, as in the version.dll build: it waits for N3.dll, which must not hold up the host's start-up.
    aorf::StartClientPatch();
    return &Plugin;
}
