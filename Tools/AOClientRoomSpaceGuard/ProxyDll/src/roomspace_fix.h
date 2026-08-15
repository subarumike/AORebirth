#pragma once

namespace aorf
{
    enum class ClientProfile
    {
        Unknown,
        NewClient,
        OldClient
    };

    ClientProfile GetLoadedN3ClientProfile();
    bool InstallClientCrashMitigation();
    bool RunClientCrashMitigationSelfTest();
}
