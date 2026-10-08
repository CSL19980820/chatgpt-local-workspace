import { cn } from "cn"

// shadcn/ui input; look in dashboard.css (.field), focus uses the shared ring.
function Input({ className, type, ...props }) {
  return <input type={type} data-slot="input" className={cn("field", className)} {...props} />
}

export { Input }
