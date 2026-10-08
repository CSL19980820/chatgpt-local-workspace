import { cn } from "cn"

// shadcn/ui textarea; look in dashboard.css (.field).
function Textarea({ className, ...props }) {
  return <textarea data-slot="textarea" className={cn("field field-area", className)} {...props} />
}

export { Textarea }
