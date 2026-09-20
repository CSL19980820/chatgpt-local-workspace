// Outline icons come from lucide-react (ISC). The name map keeps the rest of the code
// readable and lets tests look icons up by name.
import {
  Image, Layers, PanelLeft, ExternalLink, RefreshCw, Hash, Plus, Minus, ListChecks, ChevronDown,
  ChevronRight, Search, Terminal, File, FilePen, FileText, Folder, GitBranch, Info,
  TriangleAlert, Check, Circle, CircleDot, LoaderCircle, Copy, X, Clock, Activity, Timer,
} from 'lucide-react';

const ICONS = {
  image: Image, layers: Layers, dot: CircleDot, circle: Circle, check: Check, x: X, loader: LoaderCircle,
  chevron: ChevronDown, chevronRight: ChevronRight, plus: Plus, minus: Minus, hash: Hash,
  alert: TriangleAlert, info: Info, file: File, filePen: FilePen, fileText: FileText,
  search: Search, terminal: Terminal, folder: Folder, listChecks: ListChecks, refresh: RefreshCw,
  panelLeft: PanelLeft, external: ExternalLink, copy: Copy, git: GitBranch, clock: Clock,
  activity: Activity, timer: Timer,
};

export function Icon({ name, className, size = 14 }) {
  const Component = ICONS[name] || Info;
  return <Component size={size} className={className} aria-hidden="true" />;
}

export function SpinIcon({ className, size = 14 }) {
  return <LoaderCircle size={size} className={className ? 'spin ' + className : 'spin'} aria-hidden="true" />;
}
