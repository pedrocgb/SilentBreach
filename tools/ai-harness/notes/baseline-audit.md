Read AGENTS.md first.

Do not edit anything.

Run a project audit based on the harness rules.

I want a baseline report of:
1. Direct Unity Input usage instead of Rewired.
2. Same-object components serialized in scripts.
3. Persistent/global objects serialized directly.
4. Missing method comments.
5. Obsolete Unity APIs.
6. Repeated code.
7. Large classes that violate Single Responsibility.
8. Runtime object searches like FindObjectOfType or GameObject.Find.
9. AI scripts that should use A* Pathfinding Project patterns.
10. Animation/tweening code that may be better handled with DOTween.

Output the result as a refactor roadmap.
Do not modify files.
