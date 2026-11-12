const WINDOWS_1252_BYTE_BY_CODE_POINT = new Map([
  [0x20ac, 0x80], [0x201a, 0x82], [0x0192, 0x83], [0x201e, 0x84],
  [0x2026, 0x85], [0x2020, 0x86], [0x2021, 0x87], [0x02c6, 0x88],
  [0x2030, 0x89], [0x0160, 0x8a], [0x2039, 0x8b], [0x0152, 0x8c],
  [0x017d, 0x8e], [0x2018, 0x91], [0x2019, 0x92], [0x201c, 0x93],
  [0x201d, 0x94], [0x2022, 0x95], [0x2013, 0x96], [0x2014, 0x97],
  [0x02dc, 0x98], [0x2122, 0x99], [0x0161, 0x9a], [0x203a, 0x9b],
  [0x0153, 0x9c], [0x017e, 0x9e], [0x0178, 0x9f],
]);

export function repairMojibake(value) {
  if (typeof value !== 'string' || !/[ÃÂÄÅ]|á[»º]|â€™/.test(value)) return value;

  try {
    const bytes = [];
    for (const character of value) {
      const codePoint = character.codePointAt(0);
      const byte = codePoint <= 0xff ? codePoint : WINDOWS_1252_BYTE_BY_CODE_POINT.get(codePoint);
      if (byte === undefined) return value;
      bytes.push(byte);
    }
    const decoded = new TextDecoder('utf-8', { fatal: true }).decode(new Uint8Array(bytes));
    return decoded && !/[ÃÂÄÅ]|á[»º]|â€™/.test(decoded) ? decoded : value;
  } catch {
    return value;
  }
}

export default repairMojibake;
