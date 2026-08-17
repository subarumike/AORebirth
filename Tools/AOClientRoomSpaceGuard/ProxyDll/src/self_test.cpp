#include "login_key_patch.h"
#include "roomspace_fix.h"

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

    std::printf("AORebirthClientPatch self-test passed: loginkey=memory-scan crash-repairs=enabled.\n");
    return 0;
}
