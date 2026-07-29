"""Check whether CastNano/53240 IDs exist in nanos.dat via dump if available."""
from pathlib import Path
import struct

# Prefer probing via unzipping MessagePack is hard; instead run a tiny extract addition.
print("Use C# nano lookup instead")
