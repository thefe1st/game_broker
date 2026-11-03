// Offline integrity/geometry checks. Does NOT execute Unity's model importer.
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const packs = [['KenneyFurniture', 10, 21], ['KenneyCityCommercial', 12, 26], ['KenneyCityRoads', 6, 14], ['KenneyCityNature', 2, 5], ['KenneyCityCars', 3, 8]];
let allModels = 0, allFiles = 0;
for (const [pack, expectedModels, expectedFiles] of packs) {
const manifest = JSON.parse(readFileSync(resolve(root, `ThirdParty/${pack}/SOURCES.json`), 'utf8'));
assert.equal(manifest.license, 'CC0-1.0');
assert.equal(manifest.files.length, expectedFiles);
let models = 0;
for (const file of manifest.files) {
  const fullPath = resolve(root, file.path);
  assert.ok(fullPath.startsWith(root + '/') || fullPath.startsWith(root + '\\'), 'Path must remain inside the project');
  const bytes = readFileSync(fullPath);
  assert.equal(bytes.length, file.bytes, file.path);
  assert.equal(createHash('sha256').update(bytes).digest('hex'), file.sha256, file.path);
  if (!file.path.endsWith('.obj')) continue;
  const vertices = [], uv = [], normals = [], faces = [], materials = new Set();
  let library;
  for (const raw of bytes.toString('utf8').split(/\r?\n/)) {
    const [kind, ...args] = raw.trim().split(/\s+/);
    if (['v', 'vt', 'vn'].includes(kind)) {
      const count = kind === 'vt' ? 2 : 3;
      assert.ok(args.length === count || (kind === 'v' && args.length === 6), `${file.path}: ${kind} coordinates/optional RGB`);
      const values = args.map(Number);
      assert.ok(values.every(Number.isFinite), `${file.path}: finite coordinates`);
      (kind === 'v' ? vertices : kind === 'vt' ? uv : normals).push(values.slice(0, count));
    } else if (kind === 'f') {
      assert.ok(args.length >= 3, `${file.path}: face needs 3 vertices`);
      faces.push(args);
    } else if (kind === 'mtllib') library = args.join(' ');
    else if (kind === 'usemtl') materials.add(args.join(' '));
  }
  assert.ok(vertices.length && faces.length && library, `${file.path}: missing geometry/library`);
  for (const face of faces) for (const point of face) {
    for (const [slot, token] of point.split('/').entries()) {
      if (!token) continue;
      const index = Number(token), limit = [vertices.length, uv.length, normals.length][slot];
      assert.ok(Number.isInteger(index) && index > 0 && index <= limit, `${file.path}: invalid OBJ index ${point}`);
    }
  }
  assert.equal(library, file.path.split('/').at(-1).replace('.obj', '.mtl'));
  const mtl = readFileSync(resolve(dirname(fullPath), library), 'utf8');
  const defined = new Set([...mtl.matchAll(/^newmtl\s+(.+?)\s*$/gm)].map(m => m[1]));
  for (const name of materials) assert.ok(defined.has(name), `${file.path}: missing material ${name}`);
  for (const map of mtl.matchAll(/^\s*map_Kd\s+(.+?)\s*$/gm)) {
    const texturePath = resolve(dirname(fullPath), map[1]);
    const texture = manifest.files.find(f => resolve(root, f.path) === texturePath);
    assert.ok(texture, `${file.path}: texture must be included in the source manifest`);
    const png = readFileSync(texturePath);
    assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
    assert.ok(png.readUInt32BE(16) > 0 && png.readUInt32BE(20) > 0, 'PNG atlas dimensions');
  }
  const size = [0, 1, 2].map(axis => Math.max(...vertices.map(v => v[axis])) - Math.min(...vertices.map(v => v[axis])));
  assert.ok(size.every(value => value > 0 && Number.isFinite(value)), `${file.path}: usable bounds`);
  const triangles = faces.reduce((sum, face) => sum + face.length - 2, 0);
  assert.ok(triangles < 5000, `${file.path}: office prop triangle budget`);
  console.log(`${pack}/${file.path.split('/').at(-1)}: ${triangles} triangles, bounds ${size.map(n => n.toFixed(3)).join(' x ')}, materials ${[...materials].join(', ')}`);
  models++;
}
assert.equal(models, expectedModels);
const license = readFileSync(resolve(root, `ThirdParty/${pack}/License.txt`), 'utf8');
assert.match(license, /CC0/);
assert.match(license, /commercial/);
allModels += models; allFiles += manifest.files.length;
}
const surfaces = JSON.parse(readFileSync(resolve(root, 'ThirdParty/CitySurfaces/SOURCES.json'), 'utf8'));
assert.equal(surfaces.license, 'CC0-1.0'); assert.equal(surfaces.files.length, 4);
for (const file of surfaces.files) {
  const packed = readFileSync(resolve(root, file.path), 'utf8'); assert.ok(packed.length <= 3000000);
  const jpeg = Buffer.from(packed.trim(), 'base64'); assert.equal(jpeg.toString('base64'), packed.trim());
  assert.equal(createHash('sha256').update(jpeg).digest('hex'), file.sourceSha256);
  assert.equal(createHash('md5').update(jpeg).digest('hex'), file.sourceMd5);
  assert.equal(jpeg.length, file.sourceBytes); assert.equal(jpeg.readUInt16BE(0), 0xffd8);
  let dimensions;
  for (let offset = 2; offset + 4 < jpeg.length;) {
    assert.equal(jpeg[offset], 0xff); const marker = jpeg[offset + 1], length = jpeg.readUInt16BE(offset + 2);
    if ([0xc0, 0xc1, 0xc2].includes(marker)) { dimensions = [jpeg.readUInt16BE(offset + 7), jpeg.readUInt16BE(offset + 5)]; break; }
    offset += 2 + length;
  }
  assert.deepEqual(dimensions, [file.width, file.height]);
}
console.log(`${allModels} models / ${allFiles} original pack files + 4 surface maps: hashes, geometry/MTL/atlas dependencies, JPEG dimensions and license declarations OK. Not a Unity import/render test.`);
