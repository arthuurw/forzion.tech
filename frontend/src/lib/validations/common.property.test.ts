/**
 * Property-based tests para validacoes zod.
 *
 * fast-check gera centenas de inputs aleatorios verificando invariantes.
 * Falha de assercao causa shrinking automatico para o menor contra-exemplo.
 *
 * Por que property tests em validators:
 * - Inputs sao essencialmente infinitos (qualquer string passa por aqui)
 * - Bugs comuns sao em edge cases (vazio, max+1, unicode, regex backtracking)
 * - Schemas zod sao funcoes puras — ideais para property testing
 */
import { describe, expect } from "vitest";
import { fc, test } from "@fast-check/vitest";
import {
  emailSchema,
  passwordSchema,
  registerPasswordSchema,
  nomeSchema,
  telefoneSchema,
  loginSchema,
} from "./common";

/**
 * Arbitrary de email conservadora — sub-conjunto de RFC que zod aceita.
 * fc.emailAddress() do fast-check gera emails RFC-validos (ex: !@a.com)
 * que regex do zod rejeita. Mantemos arbitrary mais estrita para testar
 * o que validamente passa pelos dois.
 */
const safeEmailArb = fc
  .tuple(
    // Local-part: letras/digitos, opcionalmente um separador interno simples (sem repeticao).
    fc.stringMatching(/^[a-z][a-z0-9]{0,20}$/),
    fc.stringMatching(/^[a-z][a-z0-9]{1,15}\.[a-z]{2,5}$/),
  )
  .map(([local, domain]) => `${local}@${domain}`);

describe("emailSchema", () => {
  test.prop([safeEmailArb])(
    "emails do sub-conjunto seguro sempre passam",
    (email) => {
      expect(emailSchema.safeParse(email).success).toBe(true);
    },
  );

  test.prop([fc.string({ maxLength: 0 })])(
    "string vazia sempre falha",
    (empty) => {
      expect(emailSchema.safeParse(empty).success).toBe(false);
    },
  );

  test.prop([fc.string({ minLength: 255, maxLength: 500 })])(
    "string com mais de 254 chars sempre falha",
    (tooLong) => {
      expect(emailSchema.safeParse(tooLong).success).toBe(false);
    },
  );
});

describe("passwordSchema", () => {
  test.prop([fc.string({ minLength: 8, maxLength: 200 })])(
    "qualquer string com >= 8 chars passa",
    (pwd) => {
      expect(passwordSchema.safeParse(pwd).success).toBe(true);
    },
  );

  test.prop([fc.string({ minLength: 0, maxLength: 7 })])(
    "qualquer string com < 8 chars falha",
    (pwd) => {
      expect(passwordSchema.safeParse(pwd).success).toBe(false);
    },
  );
});

describe("registerPasswordSchema", () => {
  test.prop([
    fc.tuple(
      fc.stringMatching(/^[a-z]{1,5}$/),
      fc.stringMatching(/^[A-Z]{1,5}$/),
      fc.stringMatching(/^[0-9]{1,5}$/),
      fc.stringMatching(/^[a-zA-Z0-9]{0,50}$/),
    ),
  ])(
    "senha com lower+upper+digito de tamanho [12,72] sempre passa",
    ([lower, upper, digit, filler]) => {
      const pwd = (lower + upper + digit + filler).slice(0, 72);
      if (pwd.length < 12) return; // skip — fast-check pode gerar < 12
      expect(registerPasswordSchema.safeParse(pwd).success).toBe(true);
    },
  );

  test.prop([fc.string({ minLength: 8, maxLength: 72 }).filter((s) => !/[a-z]/.test(s))])(
    "senha sem letra minuscula sempre falha",
    (pwd) => {
      expect(registerPasswordSchema.safeParse(pwd).success).toBe(false);
    },
  );

  test.prop([fc.string({ minLength: 73, maxLength: 200 })])(
    "senha com > 72 chars sempre falha",
    (pwd) => {
      expect(registerPasswordSchema.safeParse(pwd).success).toBe(false);
    },
  );
});

describe("nomeSchema", () => {
  test.prop([fc.string({ minLength: 2, maxLength: 100 })])(
    "string de 2-100 chars sempre passa",
    (nome) => {
      expect(nomeSchema.safeParse(nome).success).toBe(true);
    },
  );

  test.prop([fc.string({ minLength: 0, maxLength: 1 })])(
    "string com 0 ou 1 char sempre falha",
    (nome) => {
      expect(nomeSchema.safeParse(nome).success).toBe(false);
    },
  );

  test.prop([fc.string({ minLength: 101, maxLength: 200 })])(
    "string com > 100 chars sempre falha",
    (nome) => {
      expect(nomeSchema.safeParse(nome).success).toBe(false);
    },
  );
});

describe("telefoneSchema", () => {
  test.prop([fc.stringMatching(/^\d{10,11}$/)])(
    "string de 10 ou 11 digitos passa",
    (tel) => {
      expect(telefoneSchema.safeParse(tel).success).toBe(true);
    },
  );

  test.prop([fc.stringMatching(/^\d{1,9}$/)])(
    "string com menos de 10 digitos falha (exceto vazia)",
    (tel) => {
      // string vazia eh explicitamente permitida via .or(z.literal(""))
      const result = telefoneSchema.safeParse(tel);
      if (tel === "") {
        expect(result.success).toBe(true);
      } else {
        expect(result.success).toBe(false);
      }
    },
  );

  test.prop([fc.stringMatching(/^\d{12,20}$/)])(
    "string com mais de 11 digitos falha",
    (tel) => {
      expect(telefoneSchema.safeParse(tel).success).toBe(false);
    },
  );
});

describe("loginSchema", () => {
  test.prop([
    fc.record({
      email: safeEmailArb,
      password: fc.string({ minLength: 8, maxLength: 100 }),
    }),
  ])("email valido + password >= 8 chars sempre passa", (input) => {
    expect(loginSchema.safeParse(input).success).toBe(true);
  });

  test.prop([
    fc.record({
      email: fc.string({ maxLength: 0 }),
      password: fc.string({ minLength: 8, maxLength: 100 }),
    }),
  ])("email vazio falha mesmo com password valido", (input) => {
    expect(loginSchema.safeParse(input).success).toBe(false);
  });
});
