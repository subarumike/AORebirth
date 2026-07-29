from pathlib import Path
import sys
# Try to see if MessagePack playfields loadable - skip, just list PF ids if we can find markers
data = Path(r'AORebirth/Built/Debug/playfields.dat').read_bytes()
print('size', len(data))
# Count how many times IdentityType Terminal 0x0000C73D appears
import struct
for label, v in [('TerminalType C73D', 0xC73D), ('TerminalType 51005', 51005), ('PF655', 655), ('PF545', 545)]:
    print(label, 'LE', data.count(struct.pack('<I', v)), 'BE', data.count(struct.pack('>I', v)))
