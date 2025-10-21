import tr from '../locales/tr.json'

type Dict = Record<string, string>
const dict: Dict = tr as Dict

export function t(key: string): string {
  return dict[key] ?? key
}

