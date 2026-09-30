"use client"

import { useMutation, useQueryClient } from "@tanstack/react-query"
import { useRouter } from "next/navigation"
import { toast } from "sonner"
import { createOrder } from "../api/checkout.api"
import { cartKeys, useClearCart } from "@/features/cart"
import type { CheckoutFormValues } from "../schemas/checkout.schema"
import { messages } from "@/lib/messages.ar"

export function useCreateOrder() {
  const router = useRouter()
  const clearCart = useClearCart()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: CheckoutFormValues) => createOrder(payload),
    onSuccess: (order) => {
      clearCart()
      toast.success(messages.checkout.success)
      router.replace(
        `/order-confirmation?orderNumber=${encodeURIComponent(order.orderNumber)}`,
      )
    },
    onError: (error: Error) => {
      toast.error(error.message ?? messages.checkout.createError)
      // A failed order may have changed the cart on the server — prices that
      // moved since the items were added are refreshed there before the shopper
      // is asked to confirm — so the summary on screen must be re-read.
      void queryClient.invalidateQueries({ queryKey: cartKeys.all })
    },
  })
}
