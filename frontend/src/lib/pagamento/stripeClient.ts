import { loadStripe, type Stripe } from "@stripe/stripe-js";

const stripeKey = process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY ?? "";

let stripePromise: Promise<Stripe | null> | null = null;

export function getStripe(): Promise<Stripe | null> | null {
  if (!stripeKey) return null;
  stripePromise ??= loadStripe(stripeKey);
  return stripePromise;
}
