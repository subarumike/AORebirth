#include "login_key_patch.h"
#include "roomspace_fix.h"
#include "build_info.h"
#include "daily_login_routing.h"

#include <cstdio>

int main()
{
    if (!aorf::RunClientCrashMitigationSelfTest())
    {
        std::fprintf(stderr, "AO client crash-mitigation self-test failed.\n");
        return 1;
    }

    if (!aorf::RunLoginKeyPatchSelfTest())
    {
        std::fprintf(stderr, "AO login-key patch self-test failed.\n");
        return 1;
    }

    if (!aorf::RunDailyLoginRoutingSelfTest())
    {
        std::fprintf(stderr, "AO DailyLogin routing self-test failed.\n");
        return 1;
    }

    std::printf(
        "AORebirthClientPatch self-test passed: version=%s source=%s loginkey=memory-scan dailylogin=bounded-routing crash-repairs=enabled.\n",
        aorf::ClientPatchVersion,
        aorf::ClientPatchSourceSha);
    return 0;
}
