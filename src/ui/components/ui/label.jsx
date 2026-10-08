import { cn } from "cn"
import { Label as LabelPrimitive } from "radix-ui"

// shadcn/ui label; look in dashboard.css (.form-field label).
function Label({ className, ...props }) {
  return <LabelPrimitive.Root data-slot="label" className={cn("label", className)} {...props} />
}

export { Label }
