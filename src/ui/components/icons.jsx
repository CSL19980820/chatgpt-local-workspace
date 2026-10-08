// Outline icons come from lucide-react (ISC). The name map keeps the rest of the code
// readable and lets tests look icons up by name. One stroke weight, one gray, everywhere.
import {
  Image, Layers, PanelLeft, PanelRight, ExternalLink, RefreshCw, Hash, Plus, Minus, ListChecks, ChevronDown,
  ChevronRight, ChevronUp, Search, Terminal, SquareTerminal, File, FilePen, FilePlus, FileX, FileText, FileCode, FileJson,
  FileImage, FileCog, Folder, FolderOpen, GitBranch, GitCompare, Info, TriangleAlert, Check, Circle, CircleDot, CircleCheck,
  CircleAlert, CircleDashed, LoaderCircle, Copy, X, Clock, Activity, Timer, Trash2, Ellipsis, ArrowDown, ArrowRightLeft,
  Link, Inbox, Pause, Play, MessageSquare, Eye, WifiOff,
} from 'lucide-react';

const ICONS = {
  image: Image, layers: Layers, dot: CircleDot, circle: Circle, circleCheck: CircleCheck, circleAlert: CircleAlert,
  circleDashed: CircleDashed, check: Check, x: X, loader: LoaderCircle,
  chevron: ChevronDown, chevronRight: ChevronRight, chevronUp: ChevronUp, plus: Plus, minus: Minus, hash: Hash,
  alert: TriangleAlert, info: Info, file: File, filePen: FilePen, filePlus: FilePlus, fileX: FileX, fileText: FileText,
  fileCode: FileCode, fileJson: FileJson, fileImage: FileImage, fileCog: FileCog,
  search: Search, terminal: Terminal, squareTerminal: SquareTerminal, folder: Folder, folderOpen: FolderOpen,
  listChecks: ListChecks, refresh: RefreshCw, panelLeft: PanelLeft, panelRight: PanelRight, external: ExternalLink,
  copy: Copy, git: GitBranch, gitCompare: GitCompare, clock: Clock, activity: Activity, timer: Timer, trash: Trash2,
  more: Ellipsis, arrowDown: ArrowDown, move: ArrowRightLeft, link: Link, inbox: Inbox, pause: Pause, play: Play,
  chat: MessageSquare, eye: Eye, offline: WifiOff,
};

export function Icon({ name, className, size = 16 }) {
  const Component = ICONS[name] || Info;
  return <Component size={size} strokeWidth={1.75} className={className} aria-hidden="true" />;
}

export function SpinIcon({ className, size = 14 }) {
  return <LoaderCircle size={size} strokeWidth={2} className={className ? 'spin ' + className : 'spin'} aria-hidden="true" />;
}

// File-type glyph for a path: still one monochrome outline, only the shape changes.
const CODE = /\.(c|cc|cpp|cs|css|go|h|html?|java|jsx?|cjs|mjs|kt|php|ps1|py|rb|rs|scss|sh|sql|swift|tsx?|vue|xaml)$/i;
export function fileIcon(path) {
  const value = String(path || '').toLowerCase();
  if (/\.(json|jsonc|ya?ml|toml)$/.test(value)) return 'fileJson';
  if (/\.(png|jpe?g|gif|webp|svg|ico|bmp)$/.test(value)) return 'fileImage';
  if (/\.(config|ini|env|props|csproj|sln|lock)$/.test(value)) return 'fileCog';
  if (CODE.test(value)) return 'fileCode';
  if (/\.(md|txt|log|rst|csv)$/.test(value)) return 'fileText';
  return 'file';
}
