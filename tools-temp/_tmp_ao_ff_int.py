# Reverse AO FormatFeedback int encoding from known capture pairs.
pairs = [
    (100, '!!!"0'),
    (0, '!!!!'),
    (0, '!!!!!'),
    (10, '!!!!+'),
    (3617, '!!!KP'),
    (787, '!!!*7'),
    (2581, '!!!?O'),
    (1160, '!!!/S'),
]

def dec85(s):
    n = 0
    for c in s:
        n = n * 85 + (ord(c) - 33)
    return n

print('full-string base85 decode:')
for v, s in pairs:
    print(' ', v, repr(s), '->', dec85(s), 'ok' if dec85(s) == v else '')

# Classic AO encoder from various private servers:
# while value >= 0: emit; special 5-char padded form trimmed

def encode_ao(value):
    """Candidate: encode as base85 big-endian, always 5 chars, then strip trailing '!'?"""
    if value < 0:
        value = value & 0xFFFFFFFF
    chars = []
    n = value
    for _ in range(5):
        chars.append(chr((n % 85) + 33))
        n //= 85
    chars.reverse()
    return ''.join(chars)

print('\n5-char encode:')
for v, s in [(100, '!!!"0'), (0, '!!!!!'), (10, '!!!!+'), (3617, '!!!KP'), (787, '!!!*7'), (2581, '!!!?O'), (1160, '!!!/S')]:
    e = encode_ao(v)
    print(' ', v, 'enc', repr(e), 'want', repr(s), 'match', e == s)

# Try: encode then strip leading '!'? or trailing?
def encode_trim_leading(value):
    e = encode_ao(value)
    # keep at least 1 char?
    return e  # observe

# AO algorithm from aochat / Funcom style (recursive):
def encode_recursive(value):
    # From some CellAO forks:
    # string Encode(int i) {
    #   string s = "";
    #   if (i == 0) return "!";
    #   while (i > 0) { s = (char)((i % 85) + 33) + s; i /= 85; }
    #   return s;
    # }
    if value == 0:
        return '!'
    s = []
    i = value
    while i > 0:
        s.append(chr((i % 85) + 33))
        i //= 85
    s.reverse()
    return ''.join(s)

print('\nrecursive (no pad):')
for v, s in [(100, '!!!"0'), (0, '!!!!'), (10, '!!!!+'), (3617, '!!!KP'), (787, '!!!*7'), (2581, '!!!?O'), (1160, '!!!/S')]:
    e = encode_recursive(v)
    print(' ', v, 'enc', repr(e), 'want', repr(s), 'match', e == s)

# Maybe always prepend '!!!' fixed? Unlikely.

# Try 5-char always and compare - capture uses variable length starting with !!!
print('\n5-char vs want detail:')
for v, s in [(100, '!!!"0'), (10, '!!!!+'), (3617, '!!!KP'), (787, '!!!*7'), (2581, '!!!?O'), (1160, '!!!/S'), (0, '!!!!!')]:
    e = encode_ao(v)
    print(v, '5=', repr(e), 'want=', repr(s), 'dec5=', dec85(e), 'decwant=', dec85(s))
