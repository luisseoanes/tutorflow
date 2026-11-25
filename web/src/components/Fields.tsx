import { useId } from "react";
import type {
  InputHTMLAttributes,
  ReactElement,
  ReactNode,
  Ref,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";

interface FieldShellProps {
  id: string;
  label: string;
  hint?: ReactNode;
  error?: string | null;
  describedBy: string;
  children: ReactNode;
  footer?: ReactNode;
}

function FieldShell({ id, label, hint, error, describedBy, children, footer }: FieldShellProps): ReactElement {
  return (
    <div className={`field${error ? " field--invalid" : ""}`}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      {children}
      <div className="field__meta" id={describedBy}>
        {error ? (
          <span className="field__error">{error}</span>
        ) : hint ? (
          <span className="field__hint">{hint}</span>
        ) : (
          <span />
        )}
        {footer}
      </div>
    </div>
  );
}

interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  hint?: ReactNode;
  error?: string | null;
  inputRef?: Ref<HTMLInputElement>;
}

export function TextField({ label, hint, error, inputRef, ...rest }: TextFieldProps): ReactElement {
  const id = useId();
  const describedBy = `${id}-desc`;
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} describedBy={describedBy}>
      <input
        id={id}
        ref={inputRef}
        className="control"
        aria-describedby={describedBy}
        aria-invalid={error ? true : undefined}
        {...rest}
      />
    </FieldShell>
  );
}

interface TextAreaProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "id"> {
  label: string;
  hint?: ReactNode;
  error?: string | null;
  counter?: ReactNode;
  textareaRef?: Ref<HTMLTextAreaElement>;
}

export function TextArea({ label, hint, error, counter, textareaRef, ...rest }: TextAreaProps): ReactElement {
  const id = useId();
  const describedBy = `${id}-desc`;
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} describedBy={describedBy} footer={counter}>
      <textarea
        id={id}
        ref={textareaRef}
        className="control control--textarea"
        aria-describedby={describedBy}
        aria-invalid={error ? true : undefined}
        {...rest}
      />
    </FieldShell>
  );
}

interface SelectOption {
  value: string;
  label: string;
}

interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "id" | "children"> {
  label: string;
  options: readonly SelectOption[];
  hint?: ReactNode;
}

export function Select({ label, options, hint, ...rest }: SelectProps): ReactElement {
  const id = useId();
  const describedBy = `${id}-desc`;
  return (
    <FieldShell id={id} label={label} hint={hint} describedBy={describedBy}>
      <select id={id} className="control control--select" aria-describedby={describedBy} {...rest}>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </FieldShell>
  );
}
