export const ZPL_PLACEHOLDER = '{{DEGER}}'

export const ZPL_TEMPLATE =
  '\x10CT~~CD,~CC^~CT~\n' +
  '^XA~TA000~JSN^LT0^MNW^MTT^PON^PMN^LH0,0^JMA^PR4,4~SD30^JUS^LRN^CI0^XZ\n' +
  '^XA\n' +
  '^MMT\n' +
  '^PW609\n' +
  '^LL0080\n' +
  '^LS0\n' +
  '^FO64,32^GFA,00512,00512,00008,:Z64:\n' +
  'eJxjYBjiQJ6//QCItp9//gGILtxxJwFEMzYcANP87M0HkGnmxgMPkOUTN9wA08U774DF7eX7werk+NkbaO32QQAAq1gWEg==:7968\n' +
  '^FO224,32^GFA,00512,00512,00008,:Z64:\n' +
  'eJxjYBhqQJ6//QCItp9//gGILtxxJwFEMzYcANP87M0HkGnmxgMPkOUTN9wA08U774DF7eX7werk+NkbaO32wQcA9mYWEg==:1948\n' +
  '^BY1,3,19^FT66,28^BCN,,N,N\n' +
  '^FD>;123456789012^FS\n' +
  '^BY1,3,20^FT238,29^BCN,,N,N\n' +
  '^FD>;123456789012^FS\n' +
  '^FT176,68^A0N,17,16^FH\\^FDgr^FS\n' +
  '^FT343,70^A0N,17,16^FH\\^FDgr^FS\n' +
  '^FT111,68^A0N,19,24^FH\\^FD{{DEGER}}^FS\n' +
  '^FT278,68^A0N,19,24^FH\\^FD{{DEGER}}^FS\n' +
  '^FT52,49^A0N,17,16^FH\\^FDEREN KUYUMCULUK^FS\n' +
  '^FT219,49^A0N,17,16^FH\\^FDEREN KUYUMCULUK^FS\n' +
  '^PQ1,0,1,Y^XZ\n'

export function buildZplPreview(value: string) {
  const safe = value.trim() || ZPL_PLACEHOLDER
  return ZPL_TEMPLATE.replaceAll(ZPL_PLACEHOLDER, safe)
}

