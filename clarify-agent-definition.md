En un flujo Spec-Driven, las Open Questions no deberían quedar como simples preguntas pendientes si queremos pasar de Specify → Plan → Tasks. Son puntos de ambigüedad que pueden afectar comportamiento, arquitectura, pruebas o contrato de API.

La idea es que el agente no invente respuestas. Primero identifica las preguntas; después, mediante una fase de clarificación, se resuelven y la especificación se actualiza.

1. Clasificar las preguntas

Las que tienes son muy buenas, pero podemos clasificarlas:

Pregunta	Impacto	Quién debería resolverla
Status/response para email duplicado	API / negocio	Product Owner + API owner
Unicidad case-sensitive	Regla de negocio	Product Owner
Validación/normalización email	Negocio + técnica	Product Owner + técnico
Nombres solo con espacios	Validación	Product Owner
ID malformado: 400/404	API contract	API owner
Autorización	Seguridad	Product Owner + Security/Architecture

La última, por ejemplo, puede tener un impacto mucho mayor que las demás porque puede cambiar el diseño de seguridad.

2. No deberíamos ejecutar Plan todavía

Este es un punto importante para mejorar nuestro workflow.

Si tenemos:

spec.md
   │
   ├── Requirements
   ├── Acceptance Criteria
   └── Open Questions ❗

y todavía existen preguntas que cambian el comportamiento del sistema, no deberíamos pasar directamente a Plan.

El flujo debería ser:

Specify
   ↓
Open Questions
   ↓
Clarify
   ↓
Updated Specification
   ↓
Plan

Por eso, para nuestro paquete de agentes, yo agregaría un sexto agente:

speckit-clarify.agent.md

entre Specify y Plan.

3. ¿Cómo debería funcionar Clarify?

El agente debe tomar:

spec.md

y encontrar:

preguntas abiertas;
ambigüedades;
contradicciones;
requisitos incompletos;
comportamientos no definidos que puedan afectar implementación.

Por ejemplo:

## Open Questions

- What HTTP status and response contract should be used when creation is
  rejected because the email already exists?

- Is email uniqueness case-sensitive or case-insensitive?

...

El agente no debería responderlas por sí mismo.

Debería presentarlas al usuario de manera estructurada.

Por ejemplo:

Q1 — Duplicate email

What should happen when a customer is created using an existing email?

A. 409 Conflict

{
  "code": "CUSTOMER_EMAIL_EXISTS",
  "message": "A customer with this email already exists."
}

B. 400 Bad Request

{
  "code": "VALIDATION_ERROR",
  "message": "Email is already registered."
}

C. Otro comportamiento.

4. Una vez que tú respondes, Clarify actualiza la especificación

Supongamos que decides:

A. Use 409 Conflict.

Entonces la pregunta desaparece de Open Questions.

Y aparece una regla concreta:

### BR-001 — Email uniqueness

Customer email addresses must be unique.

If a customer is created using an email that already exists,
the API shall return HTTP 409 Conflict.

The response shall contain:

{
  "code": "CUSTOMER_EMAIL_EXISTS",
  "message": "A customer with this email already exists."
}

Y podemos actualizar los acceptance criteria:

### AC-005 — Duplicate email

Given that a customer already exists with email
`john@example.com`,

when another customer is created using
`john@example.com`,

then the API shall return HTTP 409 Conflict,

and the response code shall be
`CUSTOMER_EMAIL_EXISTS`.

Ahora la pregunta ya no es una ambigüedad.

Tenemos:

Question
   ↓
Decision
   ↓
Business Rule
   ↓
Acceptance Criterion
   ↓
Implementation Task
   ↓
Test
5. Aplicándolo a tus seis preguntas

Yo las resolvería una por una, porque algunas requieren decisión del negocio.

Q1 — Email duplicado

Debe definirse:

HTTP status
Response body
Error code

Por ejemplo:

409 Conflict
CUSTOMER_EMAIL_EXISTS

Pero esto es una decisión del proyecto, no algo que el agente debería inventar.

Q2 — Case sensitivity

Hay que decidir explícitamente:

John@example.com
john@example.com
JOHN@EXAMPLE.COM

¿Son el mismo customer?

Una posible decisión sería:

Email uniqueness is case-insensitive.

Pero nuevamente: no debemos imponerla automáticamente.

Q3 — Email validation

Esta pregunta es particularmente importante porque "valid email" puede significar muchas cosas.

Hay que definir al menos:

Validation
Normalization
Storage
Comparison

Por ejemplo:

Input:
"  John@Example.COM  "

Normalization:
trim whitespace

Stored:
john@example.com

Comparison:
case-insensitive

Pero eso debe ser una decisión explícita.

Q4 — Whitespace-only names

Por ejemplo:

{
  "firstName": "   ",
  "lastName": "Smith"
}

Hay que decidir si:

"   "

es equivalente a:

null

o si simplemente es un string técnicamente válido.

Para un sistema real, esto debería quedar especificado y probado.

Q5 — Customer ID inválido

Aquí hay que distinguir dos situaciones:

GET /customers/abc

si el ID debe ser un GUID.

Eso podría ser:

400 Bad Request

mientras que:

GET /customers/550e8400-e29b-41d4-a716-446655440000

cuando el GUID es válido pero no existe podría ser:

404 Not Found

Esta distinción es excelente para nuestro experimento porque prueba que el agente puede convertir una ambigüedad en dos acceptance criteria diferentes.

Q6 — Authorization

Esta pregunta probablemente debería tratarse como una decisión arquitectónica/security, no simplemente como una pregunta de implementación.

Tenemos que definir:

Authentication mechanism
Authorization policy
Protected endpoints
Unauthenticated response
Unauthorized response
Forbidden response

Por ejemplo:

401 Unauthorized

para una solicitud sin autenticación válida y:

403 Forbidden

para un usuario autenticado pero sin permisos.

Pero nuevamente, la especificación debe reflejar la política real del proyecto.

6. Lo más importante: distinguir preguntas bloqueantes

No todas las Open Questions tienen que bloquear Plan.

Podemos agregar una clasificación:

## Open Questions

| ID | Question | Impact | Blocking |
|---|---|---|---|
| OQ-001 | Duplicate email status | API contract | Yes |
| OQ-002 | Email case sensitivity | Business rule | Yes |
| OQ-003 | Email normalization | Business/API | Yes |
| OQ-004 | Whitespace names | Validation | Yes |
| OQ-005 | Invalid ID response | API contract | Yes |
| OQ-006 | Authorization | Security | Yes |

Entonces el agente puede determinar:

BLOCKED

si existe alguna pregunta Blocking = Yes.

Y solamente:

READY_FOR_PLAN

cuando las preguntas bloqueantes estén resueltas.

7. Esto mejora nuestro contrato entre agentes

Actualmente tenemos:

Specify → Plan

Yo lo cambiaría a:

Specify
   ↓
Clarify
   ↓
Plan
   ↓
Tasks
   ↓
Implement
   ↓
Converge

Y los handoffs:

Specify
   │
   └──→ Clarify

Clarify
   │
   └──→ Plan

Plan
   │
   └──→ Tasks

Tasks
   │
   └──→ Implement

Implement
   │
   └──→ Converge

Converge
   │
   └──→ Implement

Con una condición importante:

Clarify → Plan

solo cuando no existan preguntas bloqueantes.

8. Incluso podemos añadir estados al spec.md

Esto haría el sistema bastante más robusto.

Por ejemplo:

status: DRAFT
clarification_status: BLOCKED

Después de resolver las preguntas:

status: APPROVED
clarification_status: RESOLVED

Entonces Plan puede comprobar:

specification.status == APPROVED
AND
specification.clarification_status == RESOLVED

antes de comenzar.

Esto evita que Copilot diga:

"No está definido, así que asumiré que..."

que es precisamente uno de los problemas que queremos evitar en Spec-Driven Development.

Mi recomendación para nuestro experimento

Antes de continuar con Plan, hagamos una pequeña modificación al paquete que creamos:

Specify
   ↓
Clarify  ← nuevo
   ↓
Plan
   ↓
Tasks
   ↓
Implement
   ↓
Converge

y hagamos que Clarify convierta esas seis preguntas reales de Customer en decisiones, requisitos, reglas y acceptance criteria.

Así nuestro proyecto CustomerDemo probará algo mucho más interesante que simplemente "cinco agentes generan código": probará que el sistema puede manejar ambigüedad → decisión → trazabilidad → código → pruebas → convergencia.