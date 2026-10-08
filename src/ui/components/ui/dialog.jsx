import { useRef } from "react"
import { cn } from "cn"
import { XIcon } from "lucide-react"
import { Dialog as DialogPrimitive } from "radix-ui"

// shadcn/ui dialog (new-york). Styling lives in dashboard.css (.dialog-*): the whole
// dialog scrolls inside the window, so nothing is clipped in a short WebView.
function Dialog(props) {
  return <DialogPrimitive.Root data-slot="dialog" {...props} />
}

function DialogTrigger(props) {
  return <DialogPrimitive.Trigger data-slot="dialog-trigger" {...props} />
}

function DialogClose(props) {
  return <DialogPrimitive.Close data-slot="dialog-close" {...props} />
}

function DialogContent({ className, children, showCloseButton = true, ...props }) {
  // Focus the dialog itself on open (screen readers still announce it) instead of
  // lighting up the close button's focus ring on every opening.
  const ref = useRef(null)
  return (
    <DialogPrimitive.Portal data-slot="dialog-portal">
      <DialogPrimitive.Overlay data-slot="dialog-overlay" className="dialog-overlay" />
      <DialogPrimitive.Content ref={ref} tabIndex={-1} data-slot="dialog-content" className={cn("dialog-content", className)}
        onOpenAutoFocus={event => { event.preventDefault(); ref.current && ref.current.focus() }} {...props}>
        {children}
        {showCloseButton && (
          <DialogPrimitive.Close data-slot="dialog-close" className="dialog-close" aria-label="关闭">
            <XIcon size={16} strokeWidth={1.75} aria-hidden="true" />
            <span className="sr-only">关闭</span>
          </DialogPrimitive.Close>
        )}
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  )
}

const DialogHeader = ({ className, ...props }) => <div data-slot="dialog-header" className={cn("dialog-header", className)} {...props} />
const DialogFooter = ({ className, ...props }) => <div data-slot="dialog-footer" className={cn("dialog-footer", className)} {...props} />

function DialogTitle({ className, ...props }) {
  return <DialogPrimitive.Title data-slot="dialog-title" className={cn("dialog-title", className)} {...props} />
}

function DialogDescription({ className, ...props }) {
  return <DialogPrimitive.Description data-slot="dialog-description" className={cn("dialog-description", className)} {...props} />
}

export { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger }
