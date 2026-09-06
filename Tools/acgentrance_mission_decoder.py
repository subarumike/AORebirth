"""Offline QuestAlternative field verifier. Never assigns destination identities.

Opaque spans use the captured AOSharp schema lengths and must match byte-for-byte.
This is deliberately not a general-purpose replacement for the client parser.
"""
import base64
import struct
import math


def f32(value):
    return struct.unpack('>f', struct.pack('>f', value))[0]


class Cursor:
    def __init__(self, data):
        self.data, self.pos = data, 0

    def take(self, count):
        if count < 0 or self.pos + count > len(self.data):
            raise ValueError(f'Truncated field at {self.pos}, width {count}')
        value = self.data[self.pos:self.pos+count]
        self.pos += count
        return value

    def read(self, fmt):
        values = struct.unpack('>'+fmt, self.take(struct.calcsize('>'+fmt)))
        return values[0] if len(values) == 1 else list(values)

    def expect(self, data, field):
        offset = self.pos
        actual = self.take(len(data))
        if actual != data:
            raise ValueError(f'{field} mismatch at {offset}: {actual.hex()} != {data.hex()}')


def identity(value):
    return struct.pack('>II', value['type'] & 0xffffffff, value['instance'] & 0xffffffff)


def decode_worldpos(raw):
    if len(raw) != 28:
        raise ValueError('WorldPos wire width must be 28')
    typ, inst, ox, oz, x, y, z = struct.unpack('>IIii3f', raw)
    if not all(math.isfinite(v) for v in (x,y,z)):
        raise ValueError('Nonfinite WorldPos coordinate')
    return {'playfield_identity': {'type': typ, 'instance': inst},
            'playfield_origin_integer_xz': [ox, oz],
            'local_position': [x, y, z],
            'world_position': [f32(f32(ox)+x), y, f32(f32(oz)+z)],
            'raw_hex': raw.hex(), 'local_float32_hex': raw[16:].hex(),
            'operational_entrance_key': None}


def decode_cohort(raw, offers):
    if len(raw)<51 or struct.unpack_from('>H',raw,6)[0]!=len(raw):
        raise ValueError('Invalid complete AO packet length')
    c = Cursor(raw)
    c.take(16)
    if c.read('I') != 0x5c436609:
        raise ValueError('Not QuestAlternative')
    character = c.read('II')
    c.take(1)  # N3 envelope byte
    version = c.read('B')
    sliders = c.read('7B')
    seed = c.read('I')
    origin_kind = c.read('B')
    terminal_offset = c.pos
    terminal = c.read('II')
    count = c.read('B')
    if count != len(offers) or count > 5:
        raise ValueError('Cohort count mismatch')
    result = []
    for offer in offers:
        start = c.pos
        c.expect(identity(offer['mission_identity']), 'mission_identity')
        chunks = offer.get('unknown_fields', {})
        def chunk(n):
            key = f'UnkChunk{n}Base64'
            if key not in chunks:
                raise ValueError(f'Missing captured opaque span {key}')
            c.expect(base64.b64decode(chunks[key], validate=True), key)
        chunk(1)
        title = c.take(32)
        desc_len = c.read('I')
        description_offset = c.pos
        description = c.take(desc_len)
        expected_description = offer['description'].encode('utf-8')
        if description not in (expected_description, expected_description+b'\0'):
            raise ValueError('Captured description differs from wire bytes')
        if terminal != [offer['terminal_identity']['type'] & 0xffffffff, offer['terminal_identity']['instance'] & 0xffffffff]:
            raise ValueError('Header/per-offer terminal mismatch')
        c.expect(identity(offer['terminal_identity']), 'per_offer_terminal')
        c.expect(struct.pack('>I', offer['reward_descriptor_version']), 'reward_version')
        c.expect(struct.pack('>I', offer['credits']), 'credits')
        c.expect(struct.pack('>I', chunks['Unk1']), 'Unk1')
        c.expect(struct.pack('>I', offer['xp_reward']), 'xp_reward')
        chunk(2)
        marker = c.read('I')
        if not marker or marker % 1009:
            raise ValueError('Invalid reward count')
        items = offer['mission_items']
        if marker//1009-1 != len(items):
            raise ValueError('Reward count mismatch')
        for item in items:
            c.expect(struct.pack('>4I', item['low_id'], item['high_id'], item['ql'], item['unknown']), 'reward_item')
        chunk(3)
        c.expect(struct.pack('>I', offer['mission_icon']), 'mission_icon')
        chunk(4)
        destination_offset = c.pos
        # Native action object layout is 0x6C bytes before WorldPos. The retained
        # harvester schema presents one action (0x3F1 count = 0x7E2). Its first
        # DWORD varies by action; the native reader does not call it a version.
        if raw[destination_offset-112:destination_offset-108] != struct.pack('>I',2018):
            raise ValueError('Unsupported quest action count before WorldPos')
        world = decode_worldpos(c.take(28))
        if identity(world['playfield_identity']) != identity(offer['playfield']):
            raise ValueError('Destination playfield mismatch')
        if bytes.fromhex(world['raw_hex'])[8:16] != base64.b64decode(chunks['UnkChunk5Base64'], validate=True):
            raise ValueError('WorldPos origin differs from captured UnkChunk5')
        expected_position = struct.pack('>3f', *(offer['location'][a] for a in 'xyz'))
        if bytes.fromhex(world['local_float32_hex']) != expected_position:
            raise ValueError('Destination coordinate mismatch')
        chunk(6)
        result.append({'offer_index': offer['offer_index'], 'offer_start': start, 'offer_end': c.pos,
            'mission_identity': offer['mission_identity'], 'worldpos_offset': destination_offset,
            'quest_action_start': destination_offset-108,
            'quest_action_header_value': struct.unpack_from('>I',raw,destination_offset-108)[0],
            'worldpos': world, 'description_offset': description_offset, 'description_length': desc_len,
            'description_raw_hex': description.hex(), 'title_raw_hex': title.hex(),
            'decoder_status': 'EXACT_CAPTURE_SCHEMA_ASSISTED_BYTE_VERIFICATION',
            'request_terminal_identity': {'type': terminal[0], 'instance': terminal[1]},
            'request_terminal_packet_offset': terminal_offset,
            'terminal_destination_distinct_fields': True})
    if c.pos != len(raw):
        raise ValueError(f'Unparsed cohort tail at {c.pos}: {len(raw)-c.pos}')
    return {'version': version, 'sliders': sliders, 'seed': seed, 'origin_kind': origin_kind,
            'character_identity': character, 'offers': result}
