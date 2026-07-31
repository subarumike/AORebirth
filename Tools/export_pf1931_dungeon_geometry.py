import argparse
import base64
import hashlib
import json
import math
import pathlib
import re
import struct
import zlib


class RdbReader:
    def __init__(self, db_dir):
        self.db_dir = pathlib.Path(db_dir)
        self.dat_paths = sorted(
            self.db_dir.glob("ResourceDatabase.dat*"),
            key=lambda value: (
                0 if value.name == "ResourceDatabase.dat" else 1,
                value.name,
            ),
        )
        self.idx = (self.db_dir / "ResourceDatabase.idx").read_bytes()
        self.block_offset = struct.unpack_from("<I", self.idx, 12)[0]
        self.data_file_size = struct.unpack_from("<I", self.idx, 184)[0]
        self.records = {}
        offset = struct.unpack_from("<I", self.idx, 72)[0]
        while offset:
            next_offset = struct.unpack_from("<I", self.idx, offset)[0]
            count = struct.unpack_from("<h", self.idx, offset + 8)[0]
            cursor = offset + 28
            for _ in range(count):
                high, low = struct.unpack_from("<II", self.idx, cursor)
                record_type = struct.unpack_from(">i", self.idx, cursor + 8)[0]
                instance = struct.unpack_from(">i", self.idx, cursor + 12)[0]
                self.records[(record_type, instance)] = (high << 32) | low
                cursor += 16
            offset = next_offset

    def _segment_read(self, count, position):
        data_file = position // self.data_file_size
        local_position = position
        if data_file:
            local_position -= (self.data_file_size - self.block_offset) * data_file
        result = bytearray()
        remaining = count
        while remaining:
            with self.dat_paths[data_file].open("rb") as stream:
                stream.seek(local_position)
                chunk = stream.read(remaining)
            if not chunk:
                raise EOFError((data_file, local_position, remaining))
            result.extend(chunk)
            remaining -= len(chunk)
            data_file += 1
            local_position = self.block_offset
        return bytes(result)

    def get(self, record_type, instance):
        position = self.records[(record_type, instance)]
        header = self._segment_read(34, position)
        actual_type = struct.unpack_from("<i", header, 10)[0]
        actual_instance = struct.unpack_from("<i", header, 14)[0]
        total_length = struct.unpack_from("<I", header, 18)[0]
        if actual_type != record_type or actual_instance != instance:
            raise ValueError((actual_type, actual_instance))
        return self._segment_read(total_length - 12, position + 34)


def paeth(left, up, upper_left):
    value = left + up - upper_left
    left_distance = abs(value - left)
    up_distance = abs(value - up)
    upper_left_distance = abs(value - upper_left)
    if left_distance <= up_distance and left_distance <= upper_left_distance:
        return left
    if up_distance <= upper_left_distance:
        return up
    return upper_left


def decode_png(payload, position):
    cursor = position + 8
    compressed = bytearray()
    width = height = bit_depth = color_type = None
    end = None
    while cursor + 12 <= len(payload):
        length = struct.unpack_from(">I", payload, cursor)[0]
        chunk_type = payload[cursor + 4 : cursor + 8]
        chunk = payload[cursor + 8 : cursor + 8 + length]
        cursor += 12 + length
        if chunk_type == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack_from(">IIBB", chunk)
        elif chunk_type == b"IDAT":
            compressed.extend(chunk)
        elif chunk_type == b"IEND":
            end = cursor
            break
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    bytes_per_pixel = max(1, channels * bit_depth // 8)
    stride = (width * channels * bit_depth + 7) // 8
    filtered = zlib.decompress(bytes(compressed))
    pixels = bytearray(height * stride)
    source = 0
    for row in range(height):
        filter_type = filtered[source]
        source += 1
        row_start = row * stride
        previous_start = row_start - stride
        for column in range(stride):
            raw = filtered[source]
            source += 1
            left = pixels[row_start + column - bytes_per_pixel] if column >= bytes_per_pixel else 0
            up = pixels[previous_start + column] if row else 0
            upper_left = (
                pixels[previous_start + column - bytes_per_pixel]
                if row and column >= bytes_per_pixel
                else 0
            )
            if filter_type == 1:
                raw += left
            elif filter_type == 2:
                raw += up
            elif filter_type == 3:
                raw += (left + up) // 2
            elif filter_type == 4:
                raw += paeth(left, up, upper_left)
            elif filter_type != 0:
                raise ValueError(filter_type)
            pixels[row_start + column] = raw & 0xFF
    return {
        "end": end,
        "width": width,
        "height": height,
        "bit_depth": bit_depth,
        "color_type": color_type,
        "pixels": bytes(pixels),
    }


def parse_dungeon_playfield(payload):
    room_count = struct.unpack_from("<i", payload, 48)[0]
    compressed_positions = []
    for signature in (b"x\xda", b"x\x9c"):
        position = payload.find(signature)
        while position >= 0:
            try:
                decompressor = zlib.decompressobj()
                decompressor.decompress(payload[position:])
                decompressor.flush()
                if decompressor.eof:
                    compressed_positions.append(position)
            except zlib.error:
                pass
            position = payload.find(signature, position + 1)
    compressed_positions.sort()
    if len(compressed_positions) != room_count:
        raise ValueError(("room-stream-count", room_count, len(compressed_positions)))
    rooms = []
    room_end = 0
    for index, compressed_position in enumerate(compressed_positions):
        cursor = None
        door_count = None
        for candidate_door_count in range(32):
            candidate_cursor = compressed_position - (68 + candidate_door_count * 4)
            if candidate_cursor < 0:
                continue
            if (
                struct.unpack_from("<H", payload, candidate_cursor + 26)[0]
                != candidate_door_count
            ):
                continue
            candidate_name = payload[
                candidate_cursor
                + 28
                + candidate_door_count * 4 : candidate_cursor
                + 60
                + candidate_door_count * 4
            ].split(b"\0", 1)[0]
            if not candidate_name or any(
                character < 0x20 or character > 0x7E for character in candidate_name
            ):
                continue
            cursor = candidate_cursor
            door_count = candidate_door_count
            break
        if cursor is None:
            raise ValueError(("room-header", index, compressed_position))
        flags, room_flags = struct.unpack_from("<BB", payload, cursor)
        floor = struct.unpack_from("<H", payload, cursor + 2)[0]
        room_index = struct.unpack_from("<H", payload, cursor + 4)[0]
        local_rect = struct.unpack_from("<HHHH", payload, cursor + 6)
        position = struct.unpack_from("<fff", payload, cursor + 14)
        doors = [
            struct.unpack_from("<hh", payload, cursor + 28 + door_index * 4)
            for door_index in range(door_count)
        ]
        name_offset = cursor + 28 + door_count * 4
        name = payload[name_offset : name_offset + 32].split(b"\0", 1)[0].decode(
            "ascii"
        )
        block_length, decoded_words = struct.unpack_from(
            "<II", payload, name_offset + 32
        )
        decompressor = zlib.decompressobj()
        block = payload[compressed_position : compressed_position + block_length]
        try:
            decoded = decompressor.decompress(block)
        except zlib.error as error:
            raise ValueError(
                (index, cursor, compressed_position, block_length, block[:16].hex())
            ) from error
        decoded += decompressor.flush()
        consumed = block_length - len(decompressor.unused_data)
        trailer = struct.unpack_from(
            "<I", payload, compressed_position + consumed
        )[0]
        rooms.append(
            {
                "index": index,
                "offset": cursor,
                "rotation": (room_index & 3) * 90,
                "flags": flags,
                "room_flags": room_flags,
                "floor": floor,
                "room_index": room_index,
                "local_rect": local_rect,
                "position": position,
                "doors": doors,
                "name": name,
                "block_length": block_length,
                "decoded_words": decoded_words,
                "decoded_length": len(decoded),
                "trailer": trailer,
            }
        )
        room_end = max(room_end, compressed_position + consumed + 4)
    return rooms, room_end


def find_pngs(payload):
    result = []
    position = payload.find(b"\x89PNG\r\n\x1a\n")
    while position >= 0:
        png = decode_png(payload, position)
        result.append(png)
        position = payload.find(b"\x89PNG\r\n\x1a\n", png["end"])
    return result


def export_pf1931_geometry(reader, output_path):
    playfield = reader.get(1000001, 1931)
    tilemap = reader.get(1000009, 1930)
    rooms, room_end = parse_dungeon_playfield(playfield)
    pngs = find_pngs(tilemap)
    width, height = struct.unpack_from("<HH", tilemap, 12)
    tile_size, height_scale = struct.unpack_from("<ff", tilemap, 16)
    if (
        playfield[:4] != struct.pack("<I", 10)
        or room_end > len(playfield)
        or len(rooms) != 30
        or tilemap[:4] != b"GNDA"
        or width != 200
        or height != 200
        or tile_size != 2.0
        or abs(height_scale - 0.2) > 1.0e-6
        or len(pngs) < 2
        or pngs[0]["width"] != width
        or pngs[0]["height"] != height
        or pngs[0]["color_type"] != 0
        or pngs[1]["width"] != width
        or pngs[1]["height"] != height
        or pngs[1]["color_type"] != 0
        or 0x80 not in pngs[0]["pixels"]
    ):
        raise ValueError("PF1931 official dungeon records do not match the decoded contract")
    source_sha256 = hashlib.sha256(playfield + tilemap).hexdigest()
    document = {
        "schemaVersion": 1,
        "playfieldResource": 1931,
        "tilemapResource": 1930,
        "source": (
            "Official Anarchy Online ResourceDatabase RDBPlayfield "
            "(1000001:1931) plus RDBTilemap (1000009:1930)"
        ),
        "sourceSha256": source_sha256,
        "playfieldRecordSha256": hashlib.sha256(playfield).hexdigest(),
        "tilemapRecordSha256": hashlib.sha256(tilemap).hexdigest(),
        "collisionPixelsSha256": hashlib.sha256(pngs[0]["pixels"]).hexdigest(),
        "heightPixelsSha256": hashlib.sha256(pngs[1]["pixels"]).hexdigest(),
        "width": width,
        "height": height,
        "tileSize": tile_size,
        "heightScale": height_scale,
        "collisionDataBase64": base64.b64encode(pngs[0]["pixels"]).decode("ascii"),
        "heightDataBase64": base64.b64encode(pngs[1]["pixels"]).decode("ascii"),
        "rooms": [
            {
                "index": room["index"],
                "name": room["name"],
                "rotationDegrees": room["rotation"],
                "floor": room["floor"],
                "localRect": {
                    "minX": room["local_rect"][0],
                    "minZ": room["local_rect"][1],
                    "maxX": room["local_rect"][2],
                    "maxZ": room["local_rect"][3],
                },
                "center": {
                    "x": room["position"][0],
                    "y": room["position"][1],
                    "z": room["position"][2],
                },
                "doors": [
                    {"roomIndex": door[0], "doorIndex": door[1]}
                    for door in room["doors"]
                ],
            }
            for room in rooms
        ],
    }
    path = pathlib.Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    print(
        "exported",
        path,
        "rooms",
        len(rooms),
        "collision",
        document["collisionPixelsSha256"],
        "height",
        document["heightPixelsSha256"],
    )


def project_floor(document, reference_y, world_x, world_z):
    width = document["width"]
    tile_size = document["tileSize"]
    height_scale = document["heightScale"]
    collision = base64.b64decode(document["collisionDataBase64"])
    heights = base64.b64decode(document["heightDataBase64"])
    candidates = []
    for room in document["rooms"]:
        rect = room["localRect"]
        tiles_x = rect["maxX"] - rect["minX"]
        tiles_z = rect["maxZ"] - rect["minZ"]
        center = room["center"]
        angle = math.radians(room["rotationDegrees"])
        delta_x = world_x - center["x"]
        delta_z = world_z - center["z"]
        unrotated_x = delta_x * math.cos(angle) - delta_z * math.sin(angle)
        unrotated_z = delta_x * math.sin(angle) + delta_z * math.cos(angle)
        local_x = unrotated_x + tiles_x * tile_size * 0.5
        local_z = unrotated_z + tiles_z * tile_size * 0.5
        if (
            local_x < 0
            or local_z < 0
            or local_x >= tiles_x * tile_size
            or local_z >= tiles_z * tile_size
        ):
            continue
        cell_x = int(math.floor(local_x / tile_size))
        cell_z = int(math.floor(local_z / tile_size))
        collision_x = rect["minX"] + cell_x
        collision_z = rect["minZ"] + cell_z
        collision_value = collision[collision_z * width + collision_x]
        if collision_value == 0x80:
            continue
        room_heights = []
        for z in range(rect["minZ"], rect["maxZ"]):
            for x in range(rect["minX"], rect["maxX"]):
                value = collision[z * width + x]
                if value and value != 0x80:
                    room_heights.append(heights[z * width + x])
        minimum_height = min(room_heights)
        height_x = rect["minX"] - 1 + cell_x
        height_z = rect["minZ"] - 1 + cell_z
        fx = local_x / tile_size - cell_x
        fz = local_z / tile_size - cell_z
        h00 = heights[height_z * width + height_x]
        h10 = heights[height_z * width + height_x + 1]
        h01 = heights[(height_z + 1) * width + height_x]
        h11 = heights[(height_z + 1) * width + height_x + 1]
        if fx + fz <= 1.0:
            interpolated = h00 + (h10 - h00) * fx + (h01 - h00) * fz
        else:
            interpolated = h11 + (h01 - h11) * (1.0 - fx) + (h10 - h11) * (1.0 - fz)
        floor_y = center["y"] - minimum_height * height_scale + interpolated * height_scale
        candidates.append((abs(floor_y - reference_y), floor_y, room["index"]))
    return min(candidates) if candidates else None


def validate_spawn_seeds(content_path, geometry_path):
    document = json.loads(pathlib.Path(geometry_path).read_text(encoding="utf-8"))
    source = pathlib.Path(content_path).read_text(encoding="utf-8")
    spawns = []
    for line in source.splitlines():
        if "new SpawnSeed(" not in line:
            continue
        arguments = line.split("new SpawnSeed(", 1)[1].rsplit(")", 1)[0].split(",")
        source_identity = arguments[0].strip()
        coordinates = [
            float(re.sub(r"[fF]$", "", arguments[index].strip()))
            for index in (7, 8, 9)
        ]
        spawns.append((source_identity, coordinates))
    failures = []
    maximum_difference = 0.0
    for source_identity, coordinates in spawns:
        projected = project_floor(
            document, coordinates[1], coordinates[0], coordinates[2]
        )
        if projected is None:
            failures.append((source_identity, coordinates, "no-floor"))
            continue
        maximum_difference = max(maximum_difference, projected[0])
        if projected[0] > 0.65:
            failures.append((source_identity, coordinates, projected))
    print(
        "spawn-seeds",
        len(spawns),
        "failures",
        len(failures),
        "maximum-y-difference",
        maximum_difference,
    )
    if len(spawns) != 149 or failures:
        raise ValueError("Temple SpawnSeed geometry validation failed")


def main():
    parser = argparse.ArgumentParser(
        description=(
            "Export PF1931 room transforms and PF1930 collision/height rasters "
            "from an installed Anarchy Online ResourceDatabase."
        )
    )
    parser.add_argument("db_dir", help="Directory containing ResourceDatabase.idx/dat")
    parser.add_argument("output_path", help="PF1931 JSON artifact to create")
    parser.add_argument(
        "--validate-content",
        help="CapturedTempleOfThreeWindsContentProvider.cs to validate after export",
    )
    args = parser.parse_args()
    reader = RdbReader(args.db_dir)
    export_pf1931_geometry(reader, args.output_path)
    if args.validate_content:
        validate_spawn_seeds(args.validate_content, args.output_path)


if __name__ == "__main__":
    main()
