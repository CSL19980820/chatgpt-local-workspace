import { cn } from "cn"
import { AlertDialog as AlertDialogPrimitive } from "radix-ui"
import { buttonVariants } from "@/components/ui/button"

// shadcn/ui alert dialog (new-york): a confirmation that cannot be dismissed by clicking outside.
function AlertDialog(props) {
  return <AlertDialogPrimitive.Root data-slot="alert-dialog" {...props} />
}

function AlertDialogContent({ className, ...props }) {
  return (
    <AlertDialogPrimitive.Portal>
      <AlertDialogPrimitive.Overlay data-slot="alert-dialog-overlay" className="dialog-overlay" />
      <AlertDialogPrimitive.Content data-slot="alert-dialog-content" className={cn("dialog-content alert-dialog", className)} {...props} />
    </AlertDialogPrimitive.Portal>
  )
}

const AlertDialogHeader = ({ className, ...props }) => <div data-slot="alert-dialog-header" className={cn("dialog-header", className)} {...props} />
const AlertDialogFooter = ({ className, ...props }) => <div data-slot="alert-dialog-footer" className={cn("dialog-footer", className)} {...props} />

function AlertDialogTitle({ className, ...props }) {
  return <AlertDialogPrimitive.Title data-slot="alert-dialog-title" className={cn("dialog-title", className)} {...props} />
}

function AlertDialogDescription({ className, ...props }) {
  return <AlertDialogPrimitive.Description data-slot="alert-dialog-description" className={cn("dialog-description", className)} {...props} />
}

function AlertDialogAction({ className, variant = "default", ...props }) {
  return <AlertDialogPrimitive.Action data-slot="button" data-variant={variant} data-size="sm" className={cn(buttonVariants({ variant, size: "sm" }), className)} {...props} />
}

function AlertDialogCancel({ className, ...props }) {
  return <AlertDialogPrimitive.Cancel data-slot="button" data-variant="secondary" data-size="sm" className={cn(buttonVariants({ variant: "secondary", size: "sm" }), className)} {...props} />
}

export { AlertDialog, AlertDialogContent, AlertDialogHeader, AlertDialogFooter, AlertDialogTitle, AlertDialogDescription, AlertDialogAction, AlertDialogCancel }
