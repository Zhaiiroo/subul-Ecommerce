'use client';

import { useState } from 'react';
import { Check, Copy, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { messages } from '@/lib/messages.ar';
import type { TemporaryCredential } from '../../types';

const m = messages.adminUser.temporaryPassword;

interface TemporaryPasswordDialogProps {
  credential: TemporaryCredential | null;
  onClose: () => void;
}

/**
 * The one and only sighting of a generated password. The API returns it in the
 * create and reset responses and stores nothing but the bcrypt hash, so closing
 * this dialog is irreversible — hence no cancel affordance and a plain "done".
 */
export function TemporaryPasswordDialog({
  credential,
  onClose,
}: TemporaryPasswordDialogProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    if (!credential) return;
    try {
      await navigator.clipboard.writeText(credential.temporaryPassword);
      setCopied(true);
      toast.success(m.copied);
    } catch {
      toast.error(messages.common.error);
    }
  }

  function handleClose() {
    setCopied(false);
    onClose();
  }

  return (
    <AlertDialog open={credential !== null} onOpenChange={(open) => !open && handleClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <ShieldAlert className="text-amber-600 size-5" aria-hidden />
            {m.title}
          </AlertDialogTitle>
          <AlertDialogDescription>{m.description}</AlertDialogDescription>
        </AlertDialogHeader>

        {credential ? (
          <div className="space-y-3">
            <div className="rounded-lg border border-border/60 bg-muted/30 px-3 py-2.5">
              <p className="text-muted-foreground text-xs font-medium">
                {messages.adminUser.view.email}
              </p>
              <p className="text-sm" dir="ltr">
                {credential.email}
              </p>
            </div>

            <div className="flex items-center gap-2">
              <code
                className="bg-muted flex-1 truncate rounded-lg px-3 py-2.5 font-mono text-sm"
                dir="ltr"
              >
                {credential.temporaryPassword}
              </code>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={() => void handleCopy()}
                aria-label={m.copy}
              >
                {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
              </Button>
            </div>
          </div>
        ) : null}

        <AlertDialogFooter>
          <AlertDialogAction onClick={handleClose}>{m.done}</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
