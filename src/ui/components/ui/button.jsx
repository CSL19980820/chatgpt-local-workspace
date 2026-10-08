import { cva } from "class-variance-authority";
import { cn } from "cn"
import { Slot } from "radix-ui"

// shadcn/ui button API; the look lives in dashboard.css (.btn + data-variant/data-size),
// so every button shares one neutral palette and one focus ring.
const buttonVariants = cva("btn", {
  variants: {
    variant: { default: "", destructive: "", outline: "", secondary: "", ghost: "", link: "" },
    size: { default: "", xs: "", sm: "", lg: "", icon: "", "icon-xs": "", "icon-sm": "", "icon-lg": "" },
  },
  defaultVariants: { variant: "default", size: "default" },
})

function Button({ className, variant = "default", size = "default", asChild = false, ...props }) {
  const Comp = asChild ? Slot.Root : "button"
  return (
    <Comp
      data-slot="button"
      data-variant={variant}
      data-size={size}
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  )
}

export { Button, buttonVariants }
