import { cn } from "cn"
import { ToggleGroup as ToggleGroupPrimitive } from "radix-ui"

// Segmented control built on the shadcn/ui toggle group: one value always selected.
function ToggleGroup({ className, value, onValueChange, ...props }) {
  return (
    <ToggleGroupPrimitive.Root
      data-slot="toggle-group"
      type="single"
      value={value}
      onValueChange={next => { if (next) onValueChange(next) }}
      className={cn("segmented", className)}
      {...props}
    />
  )
}

function ToggleGroupItem({ className, ...props }) {
  return <ToggleGroupPrimitive.Item data-slot="toggle-group-item" className={cn("segment", className)} {...props} />
}

export { ToggleGroup, ToggleGroupItem }
