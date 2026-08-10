import { render } from "@testing-library/react";
import { axe } from "vitest-axe";
import { describe, it, expect } from "vitest";
import CheckoutTermos from "./CheckoutTermos";

describe("CheckoutTermos a11y", () => {
  it("variante default sem violações", async () => {
    const { container } = render(<CheckoutTermos valor={120} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("variante dense sem violações", async () => {
    const { container } = render(<CheckoutTermos valor={99.9} dense />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
