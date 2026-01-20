import type { ButtonHTMLAttributes } from 'react';
import { cn } from '../cn.ts';

/** Visual style of a `Button`. */
export type ButtonVariant = 'primary' | 'secondary';

/** Native button props plus `variant`. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
}

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-primary-foreground hover:opacity-90',
  secondary: 'bg-surface text-text border border-border hover:bg-surface-sunken',
};

/**
 * Compact pill action (master spec §32.4): primary = pine fill, secondary = white with border. Defaults to
 * `type="button"` so a button inside a form never submits it by accident.
 */
export function Button({ variant = 'primary', className, type = 'button', ...props }: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        'inline-flex h-10 items-center justify-center gap-2 rounded-pill px-5 text-label font-medium',
        'transition-opacity duration-fast ease-standard disabled:cursor-not-allowed disabled:opacity-50',
        variants[variant],
        className,
      )}
      {...props}
    />
  );
}
